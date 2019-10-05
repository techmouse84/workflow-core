using Abp.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace WorkflowCore.Services
{
    public class WorkflowExecutor : IWorkflowExecutor
    {
        protected readonly IWorkflowRegistry _registry;
        protected readonly IServiceProvider _serviceProvider;
        protected readonly ILogger _logger;
        private readonly IExecutionResultProcessor _executionResultProcessor;
        private readonly WorkflowOptions _options;

        private IWorkflowHost Host => _serviceProvider.GetService<IWorkflowHost>();

        public WorkflowExecutor(IWorkflowRegistry registry, IServiceProvider serviceProvider, IExecutionResultProcessor executionResultProcessor, WorkflowOptions options, ILoggerFactory loggerFactory)
        {
            _serviceProvider = serviceProvider;
            _registry = registry;
            _options = options;
            _logger = loggerFactory.CreateLogger<WorkflowExecutor>();
            _executionResultProcessor = executionResultProcessor;
        }

        public async Task<WorkflowExecutorResult> Execute(WorkflowInstance workflow)
        {
            var wfResult = new WorkflowExecutorResult();

            var exePointers = new List<ExecutionPointer>(workflow.ExecutionPointers.Where(x => x.Active && (!x.SleepUntil.HasValue || x.SleepUntil < Clock.Now.ToUniversalTime())));
            var def = _registry.GetDefinition(workflow.WorkflowDefinitionId, workflow.TenantId, workflow.Version);
            if (def == null)
            {
                _logger.LogError("Workflow {0} version {1} tenant {2} is not registered", workflow.WorkflowDefinitionId, workflow.Version, workflow.TenantId);
                return wfResult;
            }

            foreach (var pointer in exePointers)
            {
                var step = def.Steps.First(x => x.Id == pointer.StepId);
                if (step != null)
                {
                    try
                    {
                        pointer.Status = PointerStatus.Running;
                        switch (step.InitForExecution(wfResult, def, workflow, pointer))
                        {
                            case ExecutionPipelineDirective.Defer:
                                continue;
                            case ExecutionPipelineDirective.EndWorkflow:
                                workflow.Status = WorkflowStatus.Complete;
                                workflow.CompleteTime = Clock.Now.ToUniversalTime();
                                continue;
                        }

                        if (!pointer.StartTime.HasValue)
                        {
                            pointer.StartTime = Clock.Now.ToUniversalTime();
                        }

                        _logger.LogDebug("Starting step {0} on workflow {1}", step.Name, workflow.Id);

                        IStepBody body = step.ConstructBody(_serviceProvider);

                        if (body == null)
                        {
                            _logger.LogError("Unable to construct step body {0}", step.BodyType.ToString());
                            pointer.SleepUntil = Clock.Now.ToUniversalTime().Add(_options.ErrorRetryInterval);
                            wfResult.Errors.Add(new ExecutionError()
                            {
                                WorkflowId = workflow.Id,
                                ExecutionPointerId = pointer.Id,
                                ErrorTime = Clock.Now.ToUniversalTime(),
                                Message = String.Format("Unable to construct step body {0}", step.BodyType.ToString())
                            });
                            continue;
                        }

                        IStepExecutionContext context = new StepExecutionContext()
                        {
                            Workflow = workflow,
                            Step = step,
                            PersistenceData = pointer.PersistenceData,
                            ExecutionPointer = pointer,
                            Item = pointer.ContextItem
                        };

                        ProcessInputs(workflow, step, body, context);

                        switch (step.BeforeExecute(wfResult, context, pointer, body))
                        {
                            case ExecutionPipelineDirective.Defer:
                                continue;
                            case ExecutionPipelineDirective.EndWorkflow:
                                workflow.Status = WorkflowStatus.Complete;
                                workflow.CompleteTime = Clock.Now.ToUniversalTime();
                                continue;
                        }

                        var result = await body.RunAsync(context);

                        if (result.Proceed)
                        {
                            ProcessOutputs(workflow, step, body);
                        }

                        _executionResultProcessor.ProcessExecutionResult(workflow, def, pointer, step, result, wfResult);
                        step.AfterExecute(wfResult, context, result, pointer);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Workflow {0} raised error on step {1} Message: {2}", workflow.Id, pointer.StepId, ex.Message);
                        wfResult.Errors.Add(new ExecutionError()
                        {
                            WorkflowId = workflow.Id,
                            ExecutionPointerId = pointer.Id,
                            ErrorTime = Clock.Now.ToUniversalTime(),
                            Message = ex.Message
                        });

                        _executionResultProcessor.HandleStepException(workflow, def, pointer, step);
                        Host.ReportStepError(workflow, step, ex);
                    }
                }
                else
                {
                    _logger.LogError("Unable to find step {0} in workflow definition", pointer.StepId);
                    pointer.SleepUntil = Clock.Now.ToUniversalTime().Add(_options.ErrorRetryInterval);
                    wfResult.Errors.Add(new ExecutionError()
                    {
                        WorkflowId = workflow.Id,
                        ExecutionPointerId = pointer.Id,
                        ErrorTime = Clock.Now.ToUniversalTime(),
                        Message = String.Format("Unable to find step {0} in workflow definition", pointer.StepId)
                    });
                }

            }
            ProcessAfterExecutionIteration(workflow, def, wfResult);
            DetermineNextExecutionTime(workflow);

            return wfResult;
        }

        private void ProcessInputs(WorkflowInstance workflow, WorkflowStep step, IStepBody body, IStepExecutionContext context)
        {
            //TODO: Move to own class
            foreach (var input in step.Inputs)
            {
                var member = (input.Target.Body as MemberExpression);

                if (member == null)
                {
                    UnaryExpression ubody = (UnaryExpression)input.Target.Body;
                    member = ubody.Operand as MemberExpression;
                }

                object resolvedValue = null;

                switch (input.Source.Parameters.Count)
                {
                    case 1:
                        resolvedValue = input.Source.Compile().DynamicInvoke(workflow.Data);
                        break;
                    case 2:
                        resolvedValue = input.Source.Compile().DynamicInvoke(workflow.Data, context);
                        break;
                    default:
                        throw new ArgumentException();
                }

                var property = step.BodyType.GetProperty(member.Member.Name);

                var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                if (CanChangeType(resolvedValue, propertyType))
                {
                    var safeValue = (resolvedValue == null) ? null : Convert.ChangeType(resolvedValue, propertyType);

                    property.SetValue(body, safeValue);
                }
                else
                {
                    property.SetValue(body, resolvedValue);
                }

            }
        }

        public static bool CanChangeType(object value, Type conversionType)
        {
            if (conversionType == null)
            {
                return false;
            }

            if (value == null)
            {
                return false;
            }

            IConvertible convertible = value as IConvertible;

            if (convertible == null)
            {
                return false;
            }

            return true;
        }

        private void ProcessOutputs(WorkflowInstance workflow, WorkflowStep step, IStepBody body)
        {
            foreach (var output in step.Outputs)
            {
                var member = (output.Target.Body as MemberExpression);
                var resolvedValue = output.Source.Compile().DynamicInvoke(body);
                var data = workflow.Data;
                data.GetType().GetProperty(member.Member.Name).SetValue(data, resolvedValue);
            }
        }

        private void ProcessAfterExecutionIteration(WorkflowInstance workflow, WorkflowDefinition workflowDef, WorkflowExecutorResult workflowResult)
        {
            var pointers = workflow.ExecutionPointers.Where(x => x.EndTime == null);

            foreach (var pointer in pointers)
            {
                var step = workflowDef.Steps.First(x => x.Id == pointer.StepId);
                step?.AfterWorkflowIteration(workflowResult, workflowDef, workflow, pointer);
            }
        }

        private void DetermineNextExecutionTime(WorkflowInstance workflow)
        {
            workflow.NextExecution = null;

            if (workflow.Status == WorkflowStatus.Complete)
                return;

            foreach (var pointer in workflow.ExecutionPointers.Where(x => x.Active && (x.Children ?? new List<string>()).Count == 0))
            {
                if (!pointer.SleepUntil.HasValue)
                {
                    workflow.NextExecution = 0;
                    return;
                }

                long pointerSleep = pointer.SleepUntil.Value.ToUniversalTime().Ticks;
                workflow.NextExecution = Math.Min(pointerSleep, workflow.NextExecution ?? pointerSleep);
            }

            if (workflow.NextExecution == null)
            {
                foreach (var pointer in workflow.ExecutionPointers.Where(x => x.Active && (x.Children ?? new List<string>()).Count > 0))
                {
                    if (workflow.ExecutionPointers.Where(x => pointer.Children.Contains(x.Id)).All(x => x.EndTime.HasValue))
                    {
                        if (!pointer.SleepUntil.HasValue)
                        {
                            workflow.NextExecution = 0;
                            return;
                        }

                        long pointerSleep = pointer.SleepUntil.Value.ToUniversalTime().Ticks;
                        workflow.NextExecution = Math.Min(pointerSleep, workflow.NextExecution ?? pointerSleep);
                    }
                }
            }

            if ((workflow.NextExecution == null) && (workflow.ExecutionPointers.All(x => x.EndTime != null)))
            {
                workflow.Status = WorkflowStatus.Complete;
                workflow.CompleteTime = Clock.Now.ToUniversalTime();
            }
        }

    }
}

﻿using Abp.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace WorkflowCore.Services
{
    public class WorkflowRegistry : IWorkflowRegistry
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly List<Tuple<string, int, int?, WorkflowDefinition>> _registry = new List<Tuple<string, int, int?, WorkflowDefinition>>();

        private readonly SettingManager _settingManager;

        public WorkflowRegistry(IServiceProvider serviceProvider, SettingManager settingManager)
        {
            _serviceProvider = serviceProvider;
            _settingManager = settingManager;
        }

        public WorkflowDefinition GetDefinition(string workflowId, int? tenantId, int? version)
        {
            if (version.HasValue)
            {
                var entry = _registry.FirstOrDefault(x => x.Item1 == workflowId && x.Item2 == version.Value && x.Item3 == tenantId );
                if (entry == null)
                {
                    return null;
                }
                
                return entry.Item4;
            }
            else
            {
                int maxVersion = _registry.Where(x => x.Item1 == workflowId && x.Item3 == tenantId).Max(x => x.Item2);
                var entry = _registry.FirstOrDefault(x => x.Item1 == workflowId && x.Item3 == tenantId && x.Item2 == maxVersion);
                if (entry == null)
                {
                    return null;
                }

                return entry.Item4;
            }
            

            

            //Get Requisition workflow from settings

            //Get Reimbursement workflow from settings
        }

        public void RegisterWorkflow(IWorkflow workflow)
        {
            if (_registry.Any(x => x.Item1 == workflow.Id && x.Item2 == workflow.Version))
            {
                throw new InvalidOperationException($"Workflow {workflow.Id} version {workflow.Version} tenant {workflow.TenantId} is already registered");
            }

            var builder = (_serviceProvider.GetService(typeof(IWorkflowBuilder)) as IWorkflowBuilder).UseData<object>();            
            workflow.Build(builder);
            var def = builder.Build(workflow.Id, workflow.Version);
            _registry.Add(new Tuple<string, int, int?, WorkflowDefinition>(workflow.Id, workflow.Version, workflow.TenantId, def));
        }

        public void RegisterWorkflow(WorkflowDefinition definition)
        {
            if (_registry.Any(x => x.Item1 == definition.Id && x.Item2 == definition.Version))
            {
                throw new InvalidOperationException($"Workflow {definition.Id} version {definition.Version} tenant {definition.TenantId} is already registered");
            }

            _registry.Add(new Tuple<string, int, int?, WorkflowDefinition>(definition.Id, definition.Version, definition.TenantId, definition));
        }

        public void RegisterWorkflow<TData>(IWorkflow<TData> workflow)
            where TData : new()
        {
            if (_registry.Any(x => x.Item1 == workflow.Id && x.Item2 == workflow.Version))
            {
                throw new InvalidOperationException($"Workflow {workflow.Id} version {workflow.Version} tenant {workflow.TenantId} is already registed");
            }

            var builder = (_serviceProvider.GetService(typeof(IWorkflowBuilder)) as IWorkflowBuilder).UseData<TData>();
            workflow.Build(builder);
            var def = builder.Build(workflow.Id, workflow.Version);
            _registry.Add(new Tuple<string, int, int?, WorkflowDefinition>(workflow.Id, workflow.Version, workflow.TenantId, def));
        }
    }
}

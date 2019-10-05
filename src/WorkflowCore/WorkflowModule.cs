﻿using Abp.Dependency;
using Abp.Modules;
using Microsoft.Extensions.ObjectPool;
using System.Reflection;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Primitives;
using WorkflowCore.Services;
using WorkflowCore.Services.BackgroundTasks;
using WorkflowCore.Services.DefinitionStorage;

namespace WorkflowCore
{
    public class WorkflowModule : AbpModule
    {
        public override void Initialize()
        {
            IocManager.RegisterAssemblyByConvention(Assembly.GetExecutingAssembly());


        }

        public override void PostInitialize()
        {
            base.PostInitialize();

            IocManager.RegisterIfNot<IQueueProvider, SingleNodeQueueProvider>(DependencyLifeStyle.Singleton);
            IocManager.RegisterIfNot<IPersistenceProvider, MemoryPersistenceProvider>(DependencyLifeStyle.Singleton);
            IocManager.RegisterIfNot<IDistributedLockProvider, SingleNodeLockProvider>(DependencyLifeStyle.Singleton);

            IocManager.RegisterIfNot<IWorkflowRegistry, WorkflowRegistry>();

            IocManager.Register<WorkflowOptions>();

            IocManager.Register<IBackgroundTask, WorkflowConsumer>(DependencyLifeStyle.Transient);
            IocManager.Register<IBackgroundTask, EventConsumer>(DependencyLifeStyle.Transient);
            IocManager.Register<IBackgroundTask, RunnablePoller>(DependencyLifeStyle.Transient);

            IocManager.Register<IWorkflowController, WorkflowController>(DependencyLifeStyle.Singleton);
            IocManager.Register<IWorkflowHost, WorkflowHost>(DependencyLifeStyle.Singleton);
            IocManager.Register<IWorkflowExecutor, WorkflowExecutor>(DependencyLifeStyle.Transient);
            IocManager.Register<IWorkflowBuilder, WorkflowBuilder>(DependencyLifeStyle.Transient);
            IocManager.Register<IExecutionResultProcessor, ExecutionResultProcessor>(DependencyLifeStyle.Transient);
            IocManager.Register<IExecutionPointerFactory, ExecutionPointerFactory>(DependencyLifeStyle.Transient);

            IocManager.Register<IPooledObjectPolicy<IPersistenceProvider>, InjectedObjectPoolPolicy<IPersistenceProvider>>(DependencyLifeStyle.Transient);
            IocManager.Register<IPooledObjectPolicy<IWorkflowExecutor>, InjectedObjectPoolPolicy<IWorkflowExecutor>>(DependencyLifeStyle.Transient);

            IocManager.RegisterIfNot<IDefinitionLoader, DefinitionLoader>();

            IocManager.Register<Foreach>();
        }
    }
}

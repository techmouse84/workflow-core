using Abp.Dependency;
using Microsoft.Extensions.DependencyInjection;
using System;
using WorkflowCore.Interface;
using WorkflowCore.Services;

namespace WorkflowCore.Models
{
    public class WorkflowOptions 
    {
        internal TimeSpan PollInterval;
        internal TimeSpan IdleTime;
        internal TimeSpan ErrorRetryInterval;
        
        public WorkflowOptions()
        {
            PollInterval = TimeSpan.FromSeconds(10);
            IdleTime = TimeSpan.FromMilliseconds(100);
            ErrorRetryInterval = TimeSpan.FromSeconds(60);            

        }

        public void UsePollInterval(TimeSpan interval)
        {
            PollInterval = interval;
        }

        public void UseErrorRetryInterval(TimeSpan interval)
        {
            ErrorRetryInterval = interval;
        }
    }
        
}

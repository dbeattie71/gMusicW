﻿// --------------------------------------------------------------------------------------------------------------------
// Outcold Solutions (http://outcoldman.com)
// --------------------------------------------------------------------------------------------------------------------

namespace OutcoldSolutions.GoogleMusic.Suites
{
    using NUnit.Framework;

    using OutcoldSolutions.GoogleMusic.Diagnostics;

    public abstract class SuitesBase 
    {
        protected IDependencyResolverContainer Container { get; private set; }

        protected ILogManager LogManager { get; private set; }

        [SetUp]
        public virtual void SetUp()
        {
            this.Container = new DependencyResolverContainer();

            using (var registration = this.Container.Registration())
            {
                 registration.Register<ILogManager>().AsSingleton<LogManager>();
            }

            this.LogManager = this.Container.Resolve<ILogManager>();
            this.LogManager.LogLevel = LogLevel.Info;
            this.LogManager.Writers.AddOrUpdate(typeof(DebugLogWriter), type => new DebugLogWriter(), (type, writer) => writer);
        }
    }
}
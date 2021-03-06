﻿using System;

using Abp.Dependency;

using Castle.Facilities.TypedFactory;
using Castle.MicroKernel.Registration;

namespace DynamicTranslator.TestBase
{
    public class TestBaseWithLocalIocManager
    {
        protected TestBaseWithLocalIocManager()
        {
            LocalIocManager = new IocManager();
            LocalIocManager.IocContainer.AddFacility<TypedFactoryFacility>();
        }

        protected IIocManager LocalIocManager { get; }

        protected T Resolve<T>()
        {
            return (T)Resolve(typeof(T));
        }

        protected object Resolve(Type typeToResolve)
        {
            return LocalIocManager.Resolve(typeToResolve);
        }

        protected void Register<T>(DependencyLifeStyle lifeStyle = DependencyLifeStyle.Singleton) where T : class
        {
            LocalIocManager.Register<T>(lifeStyle);
        }

        protected void Register<T>(T instance, DependencyLifeStyle lifeStyle = DependencyLifeStyle.Singleton) where T : class
        {
            LocalIocManager.IocContainer.Register(
                Component.For<T>().Instance(instance).ApplyLifeStyle(lifeStyle)
            );
        }


        protected void Register<TService, TImplementation>(DependencyLifeStyle lifeStyle = DependencyLifeStyle.Singleton) where TImplementation : class, TService where TService : class
        {
            LocalIocManager.Register<TService, TImplementation>();
        }

        protected void Register<TService, TImplementation>(TService instance, DependencyLifeStyle lifeStyle = DependencyLifeStyle.Singleton) where TImplementation : class, TService where TService : class
        {
            LocalIocManager.IocContainer.Register(
                Component.For<TService>().ImplementedBy<TImplementation>().Instance(instance).ApplyLifeStyle(lifeStyle)
            );
        }
    }
}

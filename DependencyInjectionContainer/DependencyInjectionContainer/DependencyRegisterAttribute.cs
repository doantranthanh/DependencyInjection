using System;

namespace DependencyInjectionContainer
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class DependencyRegisterAttribute : Attribute
    {
        public DependencyRegisterAttribute(string key, params Type[] implementedService)
        {
            LifeStyle = DependencyListStyle.Singleton;
            Key = key;
            Services = implementedService;
            Enabled = true;
        }

        public Type[] Services { get; private set; }

        public string Key { get; set; }

        public DependencyListStyle LifeStyle { get; set; }

        public bool Enabled { get; set; }
    }
}
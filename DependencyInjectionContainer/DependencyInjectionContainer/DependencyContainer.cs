using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Castle.Facilities.WcfIntegration;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.Resolvers.SpecializedResolvers;
using Castle.Windsor;
using Castle.Windsor.Installer;


namespace DependencyInjectionContainer
{
    public class DependencyContainer : IDisposable
    {
        private static DependencyContainer _instance;
        private static readonly object _synch = new object();
       

        private IWindsorContainer _continer = new WindsorContainer();

        private bool _isAutoInitialized;
        private readonly Stack<DependencyScope> _scopeStack = new Stack<DependencyScope>();

        public DependencyContainer()
        {
            InitializeContainer();
        }


        internal IWindsorContainer WindsorContainer
        {
            get { return _continer; }
        }

        public bool IsAutoInitialized
        {
            get { return (_isAutoInitialized && _continer != null); }
            set { _isAutoInitialized = value; }
        }

        public DependencyScope CurrentScope
        {
            get
            {
                try
                {
                    if (_scopeStack.Count == 0)
                    {
                        return null;
                    }

                    return _scopeStack.Peek();
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public void Dispose()
        {
            _continer.Dispose();
            _continer = null;
        }

        public static DependencyContainer GetCurrent()
        {
            if (_instance == null || _instance._continer == null)
            {
                lock (_synch)
                {
                    _instance = new DependencyContainer();
                }
            }

            return _instance;
        }

        public DependencyScope Begin()
        {
            var scope = new DependencyScope(this);
            scope.Disposed += (send, e) =>
                              {
                                  DependencyScope currentScope = CurrentScope;
                                  while (currentScope != null)
                                  {
                                      if (currentScope == send)
                                      {
                                          _scopeStack.Pop();
                                          break;
                                      }

                                      currentScope.Dispose();
                                      currentScope = CurrentScope;
                                  }
                              };

            _scopeStack.Push(scope);

            return scope;
        }

        public void RegisterAssembly(Assembly assembly)
        {
            var typeToRegister = (from x in assembly.GetTypes()
                                  let attrib = x.GetCustomAttributes(typeof (DependencyRegisterAttribute), true)
                                                .Cast<DependencyRegisterAttribute>()
                                                .FirstOrDefault()
                                  where attrib != null && attrib.Enabled
                                  select new
                                         {
                                             Type = x,
                                             Attribute = attrib
                                         });

            foreach (var type in typeToRegister)
            {
                Register(type.Type, type.Attribute.LifeStyle, type.Attribute.Key, type.Attribute.Services);
            }
        }

        public void RegisterAssemblyAutomap(Assembly assembly,
            DependencyListStyle style = DependencyListStyle.Singleton,
            List<Type> excluded = null)
        {
            var data = AllTypes.FromAssembly(assembly)
                               .Where(t => excluded == null || (!excluded.Contains(t) && !excluded.Intersect(t.GetInterfaces()).Any()))
                               .WithService
                               .AllInterfaces();

            switch (style)
            {
                case DependencyListStyle.Singleton:
                    data.LifestyleSingleton();
                    break;
                case DependencyListStyle.Transient:
                    data.LifestyleTransient();
                    break;
                case DependencyListStyle.PerWebRequest:
                    data.LifestylePerWebRequest();
                    break;
                default:
                    throw new ArgumentOutOfRangeException("style");
            }
            _continer.Register(data);
        }

        public void RegisterConfigFile(string file)
        {
            _continer.Install(Configuration.FromXmlFile(file));
        }

        public void Register(Type implementation, DependencyListStyle lifeStyle, string key, params Type[] services)
        {
            var component = Component.For(services).ImplementedBy(implementation).Named(key);
            component = SetLifeCycle(component, lifeStyle);
            _continer.Register(component);
        }

        public void Register<T>(Type type) where T : class
        {
            _continer.Register(Component.For<T>().ImplementedBy(type));
        }

        public void Register<T>(T instance) where T : class
        {
            if (instance is DependencyContainer)
            {
                return;
            }
            _continer.Register(Component.For<T>().Instance(instance));
        }

		public void Register<TEntity, IEntity>()
			where TEntity : class
			where IEntity : class
		{
			_continer.Register(Component.For(typeof(TEntity)).ImplementedBy(typeof(IEntity)).LifeStyle.Transient);
		}

        public void Register<T>(Type type, DependencyListStyle lifeStyle) where T : class
        {
            var component = Component.For<T>().ImplementedBy(type);
            component = SetLifeCycle(component, lifeStyle);
            _continer.Register(component);
        }

        public void Register<T>(Type type, DependencyListStyle lifeStyle, Action<IDictionary> parametersResolverFn)
            where T : class
        {
            var component = Component.For<T>()
                                     .ImplementedBy(type)
                                     .DependsOn((kernel, parameters) => parametersResolverFn(parameters));
            component = SetLifeCycle(component, lifeStyle);
            _continer.Register(component);
        }

        public T[] ResolveAll<T>()
        {
            if (!IsAutoInitialized)
            {
                Initialize();
            }

            var items = _continer.ResolveAll<T>();
            if (CurrentScope != null)
            {
                foreach (var item in items)
                {
                    CurrentScope.AddObject(item);
                }
            }

            return items;
        }

        public T Resolve<T>()
        {
            if (!IsAutoInitialized)
            {
                Initialize();
            }

            var item = _continer.Resolve<T>();
            if (CurrentScope != null)
            {
                CurrentScope.AddObject(item);
            }

            return item;
        }

        public T Resolve<T>(string name)
        {
            if (!IsAutoInitialized)
            {
                Initialize();
            }

            var item = _continer.Resolve<T>(name);
            if (CurrentScope != null)
            {
                CurrentScope.AddObject(item);
            }

            return item;
        }

        public object Resolve(string name, Type type)
        {
            if (!IsAutoInitialized)
            {
                Initialize();
            }

            var item = _continer.Resolve(name, type);
            if (CurrentScope != null)
            {
                CurrentScope.AddObject(item);
            }

            return item;
        }

        public object Resolve<T>(object arguments)
        {
            if (!IsAutoInitialized)
            {
                Initialize();
            }

            var item = _continer.Resolve<T>(arguments);
            if (CurrentScope != null)
            {
                CurrentScope.AddObject(item);
            }

            return item;
        }

        public object Resolve(Type type)
        {
            if (!IsAutoInitialized)
            {
                Initialize();
            }

            var item = _continer.Resolve(type);
            if (CurrentScope != null)
            {
                CurrentScope.AddObject(item);
            }

            return item;
        }

        public void Release(object obj)
        {
            _continer.Release(obj);
        }

        public void Clear()
        {
            if (_continer != null)
            {
                Dispose();
            }

            _continer = new WindsorContainer();
            _isAutoInitialized = false;

            InitializeContainer();
        }

        private void InitializeContainer()
        {
            _continer.AddFacility<WcfFacility>();

            _continer.Kernel.Resolver.AddSubResolver(new CollectionResolver(_continer.Kernel));

            _continer.Register(Component.For<DependencyContainer>().Instance(this));
        }

        public static void Initialize()
        {
            var instance = GetCurrent();
            if (instance.IsAutoInitialized)
            {
                return;
            }

            instance.IsAutoInitialized = true;
        }

        private ComponentRegistration<T> SetLifeCycle<T>(ComponentRegistration<T> component, DependencyListStyle lifeStyle) where T : class
        {
            switch (lifeStyle)
            {
                case DependencyListStyle.Singleton:
                    component = component.LifestyleSingleton();
                    break;
                case DependencyListStyle.Transient:
                    component = component.LifestyleTransient();
                    break;
                case DependencyListStyle.PerWebRequest:
                    component = component.LifestylePerWebRequest();
                    break;
            }

            return component;
        }
    }
}

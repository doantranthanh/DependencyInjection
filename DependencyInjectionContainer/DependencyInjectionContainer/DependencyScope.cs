using System;
using System.Collections.Generic;

namespace DependencyInjectionContainer
{
    public class DependencyScope : IDisposable
    {
        private List<WeakReference> _objects = new List<WeakReference>();
        private bool _disposing;

        public DependencyScope(DependencyContainer container)
        {
            Container = container;
        }

        public DependencyContainer Container { get; set; }

        public int Count
        {
            get { return _objects.Count; }
        }

        public void Dispose()
        {
            _disposing = true;
            foreach (var item in _objects)
            {
                if (item.IsAlive)
                {
                    Container.Release(item.Target);
                }
            }

            if (Disposed != null)
            {
                Disposed(this, EventArgs.Empty);
            }
            _disposing = true;
        }

        public event EventHandler Disposed;

        public void AddObject(object obj)
        {
            if (_disposing) 
            {
            }
        }
    }
}
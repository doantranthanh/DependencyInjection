using System;

namespace DependencyInjectionContainer.Exceptions
{
    public class DependencyException : Exception
    {
        public DependencyException(string message)
            : base(message) {}
    }
}
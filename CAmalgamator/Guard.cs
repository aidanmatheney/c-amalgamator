namespace CAmalgamator
{
    using System;

    using JetBrains.Annotations;

    public static class Guard
    {
        public static void NotNull<T>([NoEnumeration] T obj, string paramName) where T : class
        {
            if (obj is null)
            {
                throw new ArgumentNullException(paramName);
            }
        }
    }
}
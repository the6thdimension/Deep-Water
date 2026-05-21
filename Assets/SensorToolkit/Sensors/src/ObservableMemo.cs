using System;
using System.Collections.Generic;

namespace Micosmo.SensorToolkit {

    public static class ObservableMemo {
        public static ObservableMemo<T> Create<T>(Func<T> selector, params IObservable[] deps) => ObservableMemo<T>.Create(selector, deps);
        public static ObservableMemo<T> Create<T>(Func<T> selector, IEnumerable<IObservable> deps) => ObservableMemo<T>.Create(selector, deps);
    }

    public class ObservableMemo<T> : IObservable<T>, IDisposable {

        public static ObservableMemo<U> Create<U>(Func<U> selector, params IObservable[] deps) => Create(selector, deps);
        public static ObservableMemo<U> Create<U>(Func<U> selector, IEnumerable<IObservable> deps) => new ObservableMemo<U>(selector, deps);

        public T Value { get; private set; }

        public event Action<T, T> OnChangedValues;
        public event Action OnChanged;

        List<IObservable> dependencies = new List<IObservable>();
        Func<T> selector;

        ObservableMemo() { }

        ObservableMemo(Func<T> selector, IEnumerable<IObservable> dependencies) {
            foreach (var o in dependencies) {
                this.dependencies.Add(o);
                o.OnChanged += OnDependencyChange;
            }
            this.selector = selector;
            Value = selector();
        }

        public void Dispose() {
            selector = null;
            Value = default;
            foreach (var o in dependencies) {
                o.OnChanged -= OnDependencyChange;
            }
        }

        void OnDependencyChange() {
            var nextVal = selector();
            if (EqualityComparer<T>.Default.Equals(nextVal, Value)) {
                return;
            }
            var prevVal = Value;
            Value = nextVal;
            OnChangedValues?.Invoke(prevVal, nextVal);
            OnChanged?.Invoke();
        }
    }
}
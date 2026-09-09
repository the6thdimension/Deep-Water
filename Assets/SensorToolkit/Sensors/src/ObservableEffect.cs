using System;
using System.Collections.Generic;

namespace Micosmo.SensorToolkit {

    /**
     * A convenient way to bind a single event handler to multiple observables at once. If any of the observables are
     * changed then the action will be invoked.
     */
    public class ObservableEffect : IObservable, IDisposable {

        public static ObservableEffect Create(params IObservable[] obs) => Create(null, obs, false);
        public static ObservableEffect Create(Action action, params IObservable[] obs) => Create(action, obs, true);
        public static ObservableEffect Create(Func<Action> action, params IObservable[] obs) => Create(action, obs, true);
        public static ObservableEffect CreateNoFireImmediate(Action action, params IObservable[] obs) => Create(action, obs, false);
        public static ObservableEffect CreateNoFireImmediate(Func<Action> action, params IObservable[] obs) => Create(action, obs, false);
        public static ObservableEffect Create(Action action, IEnumerable<IObservable> obs, bool fireImmediate = true) {
            return Create(() => {
                action?.Invoke();
                return null;
            }, obs, fireImmediate);
        }
        public static ObservableEffect Create(Func<Action> action, IEnumerable<IObservable> obs, bool fireImmediate = true) {
            var instance = new ObservableEffect(action, obs);
            if (fireImmediate) {
                instance.cleanup = action?.Invoke();
            }
            return instance;
        }

        public event Action OnChanged;

        List<IObservable> observables = new List<IObservable>();
        Func<Action> action;
        Action cleanup;

        ObservableEffect() { }

        ObservableEffect(Func<Action> action, IEnumerable<IObservable> dependencies) {
            foreach (var o in dependencies) {
                observables.Add(o);
                o.OnChanged += FireEvent;
            }
            this.action = action;
        }

        public void Dispose() {
            cleanup?.Invoke();
            cleanup = null;
            action = null;
            foreach (var o in observables) {
                o.OnChanged -= FireEvent;
            }
        }

        void FireEvent() {
            cleanup?.Invoke();
            cleanup = action?.Invoke();
            OnChanged?.Invoke();
        }
    }

}
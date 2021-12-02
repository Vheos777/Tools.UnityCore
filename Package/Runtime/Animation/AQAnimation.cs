namespace Vheos.Tools.UnityCore
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using Tools.Extensions.Math;

    abstract internal class AQAnimation
    {
        // Defaults
        static internal GUID DefaultGUID
        { get; private set; }
        static internal Func<float> DefaultTimeDeltaFunc
        { get; private set; }

        // Privates
        private readonly float _duration;
        private readonly AnimationCurve _curve;
        private readonly Func<float> _timeDeltaFunc;
        private HashSet<Event> _events;
        private (float Current, float Previous) _elapsed, _curveProgress, _curveValue;
        private Func<(float, float)> GetEventValuePairFunc(EventThresholdType thresholdType)
        => thresholdType switch
        {
            EventThresholdType.Time => () => _elapsed,
            EventThresholdType.Progress => () => _curveProgress,
            EventThresholdType.Value => () => _curveValue,
            _ => () => default,
        };
        private Func<float> GetTimeDeltaFunc(TimeDeltaType timeDeltaType)
        => timeDeltaType switch
        {
            TimeDeltaType.Scaled => () => Time.deltaTime,
            TimeDeltaType.Unscaled => () => Time.unscaledDeltaTime,
            _ => () => default,
        };
        private void InitializeEvents(IEnumerable<EventInfo> eventInfos)
        {
            _events = new HashSet<Event>();
            foreach (var eventInfo in eventInfos)
                if (eventInfo.IsOnHasFinished)
                    OnHasFinished += eventInfo.Action;
                else
                    _events.Add(new Event(eventInfo.Threshold, eventInfo.Action, GetEventValuePairFunc(eventInfo.ThresholdType)));
        }

        // for QAnimation<T>
        protected float CurveValueDelta
            => _curveValue.Current - _curveValue.Previous;
        protected Action _assignInvoke;

        // for QAnimator
        internal event Action OnHasFinished;
        internal void InvokeOnHasFinished()
        => OnHasFinished?.Invoke();
        internal bool HasFinished
        => _elapsed.Current >= _duration;
        internal void Process()
        {
            _elapsed.Previous = _elapsed.Current;
            _elapsed.Current += _timeDeltaFunc();
            _curveProgress.Previous = _curveProgress.Current;
            _curveProgress.Current = _elapsed.Current.Div(_duration).ClampMax(1f);
            _curveValue.Previous = _curveValue.Current;
            _curveValue.Current = _curve.Evaluate(_curveProgress.Current);

            _assignInvoke();
            if (_events != null)
                foreach (var @event in _events)
                    @event.TryInvoke();
        }
        internal GUID GUID { get; }

        // Initializers
        protected AQAnimation(float duration)
        {
            _duration = duration;
            _curve = Qurve.ValuesByProgress;
            GUID = DefaultGUID;
            _timeDeltaFunc = GetTimeDeltaFunc(TimeDeltaType.Scaled);
        }
        protected AQAnimation(float duration, OptionalParameters optionals)
        {
            _duration = duration;
            _curve = optionals.Curve ?? Qurve.ValuesByProgress;
            GUID = optionals.GUID ?? DefaultGUID;
            _timeDeltaFunc = GetTimeDeltaFunc(optionals.TimeDeltaType ?? TimeDeltaType.Scaled);
            if (optionals.EventInfo != null)
                InitializeEvents(optionals.EventInfo);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static private void StaticInitialize()
        {
            DefaultGUID = GUID.New;
            DefaultTimeDeltaFunc = () => Time.deltaTime;
        }

        // Defines
        private class Event
        {
            // Privates
            internal void TryInvoke()
            {
                if (Test())
                    _action.Invoke();
            }
            private readonly float _threshold;
            private readonly Action _action;
            private readonly Func<(float, float)> _valuePairFunc;
            private bool Test()
            {
                (float Current, float Previous) = _valuePairFunc();
                return Previous < _threshold && Current >= _threshold
                    || Previous > _threshold && Current <= _threshold;
            }

            // Initializers
            internal Event(float threshold, Action action, Func<(float, float)> valuePairFunc)
            {
                _threshold = threshold;
                _action = action;
                _valuePairFunc = valuePairFunc;
            }
        }
    }
}
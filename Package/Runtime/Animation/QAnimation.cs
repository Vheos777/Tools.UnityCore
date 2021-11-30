namespace Vheos.Tools.UnityCore
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using Tools.Extensions.Math;

    abstract internal class AQAnimation
    {
        // Privates
        internal bool HasFinished
        => _curveTime.Current >= _duration;
        internal void Process()
        {
            _curveTime.Previous = _curveTime.Current;
            _curveTime.Current += _timeDeltaFunc();
            _curveProgress.Previous = _curveProgress.Current;
            _curveProgress.Current = _curveTime.Current.Div(_duration).ClampMax(1f);
            _curveValue.Previous = _curveValue.Current;
            _curveValue.Current = _curve.Evaluate(_curveProgress.Current);

            _assignInvoke();
            foreach (var @event in _events)
                @event.TryInvoke();
        }
        protected float GetCurveValueDelta()
        => _curveValue.Current - _curveValue.Previous;
        protected Action _assignInvoke;
        private readonly AnimationCurve _curve;
        private readonly float _duration;
        private readonly Func<float> _timeDeltaFunc;
        private readonly HashSet<QAnimationEvent> _events = new HashSet<QAnimationEvent>();
        private (float Current, float Previous) _curveTime, _curveProgress, _curveValue;
        private Func<float> GetTimeDeltaFunc(TimeDeltaType timeDeltaType)
        => timeDeltaType switch
        {
            TimeDeltaType.Scaled => () => Time.deltaTime,
            TimeDeltaType.Unscaled => () => Time.unscaledDeltaTime,
            _ => () => default,
        };
        private void InitializeEvents(IEnumerable<QAnimationEventInfo> eventInfo)
        {
            foreach (var info in eventInfo)
            {
                Func<(float, float)> valuePairFunc = info.ThresholdType switch
                {
                    AnimationEventThresholdType.Time => () => _curveTime,
                    AnimationEventThresholdType.Progress => () => _curveProgress,
                    AnimationEventThresholdType.Value => () => _curveValue,
                    _ => () => default,
                };
                QAnimationEvent newEvent = new QAnimationEvent(info.Threshold, info.Action, valuePairFunc);
                _events.Add(newEvent);
            }

        }

        // Initializers
        protected AQAnimation(AnimationCurve curve, float duration, TimeDeltaType timeDeltaType, IEnumerable<QAnimationEventInfo> eventInfos)
        {
            _curve = curve;
            _duration = duration;
            _timeDeltaFunc = GetTimeDeltaFunc(timeDeltaType);
            InitializeEvents(eventInfos);
        }
    }

    internal class QAnimation<T> : AQAnimation where T : struct
    {
        // Privates
        private Action GetAssignInvoke<TNested>(Action<TNested> assignFunc, AssignmentType assignType, TNested value, Func<float> deltaFunc) where TNested : struct
        => assignType switch
        {
            AssignmentType.Additive => new GenericArgs<TNested>(assignFunc, value) switch
            {
                GenericArgs<float> t => () => t.AssignFunc(t.Value * deltaFunc()),
                GenericArgs<Vector2> t => () => t.AssignFunc(t.Value * deltaFunc()),
                GenericArgs<Vector3> t => () => t.AssignFunc(t.Value * deltaFunc()),
                GenericArgs<Vector4> t => () => t.AssignFunc(t.Value * deltaFunc()),
                GenericArgs<Color> t => () => t.AssignFunc(t.Value * deltaFunc()),
                GenericArgs<Quaternion> t => () => t.AssignFunc(Quaternion.identity.Lerp(t.Value, deltaFunc())),
                _ => throw AnimationNotSupportedException<TNested>(assignType),
            },
            AssignmentType.Multiplicative => new GenericArgs<TNested>(assignFunc, value) switch
            {
                GenericArgs<float> t => () => t.AssignFunc(t.Value.Pow(deltaFunc())),
                GenericArgs<Vector2> t => () => t.AssignFunc(t.Value.Pow(deltaFunc())),
                GenericArgs<Vector3> t => () => t.AssignFunc(t.Value.Pow(deltaFunc())),
                GenericArgs<Vector4> t => () => t.AssignFunc(t.Value.Pow(deltaFunc())),
                GenericArgs<Color> t => () => t.AssignFunc(t.Value.Pow(deltaFunc())),
                _ => throw AnimationNotSupportedException<TNested>(assignType),
            },
            _ => throw AnimationNotSupportedException<TNested>(assignType),
        };
        private NotSupportedException AnimationNotSupportedException<TNested>(AssignmentType assignType) where TNested : struct
        => new NotSupportedException($"{assignType} {typeof(TNested).Name} animation is not supported!");

        // Initializers
        internal QAnimation(Action<T> assignFunc, AssignmentType assignType, T value, AnimationCurve curve, float duration, TimeDeltaType deltaType, IEnumerable<QAnimationEventInfo> eventInfos)
            : base(curve, duration, deltaType, eventInfos)
        => _assignInvoke = GetAssignInvoke(assignFunc, assignType, value, GetCurveValueDelta);

        // Defines
        private class GenericArgs<TNested> where TNested : struct
        {
            // Publics
            public Action<TNested> AssignFunc;
            public TNested Value;

            // Initializers
            public GenericArgs(Action<TNested> assignFunc, TNested value)
            {
                AssignFunc = assignFunc;
                Value = value;
            }
        }
    }

    public enum AssignmentType
    {
        Additive = 0,
        Multiplicative,
    }

    public enum TimeDeltaType
    {
        Scaled = 0,
        Unscaled,
    }
}
# UnityAnimationCurveEditor
Scripting resource that provides a visual spline editor for Unity AnimationCurves

# Usage Example

```cs
AnimationCurve animationCurve = new AnimationCurve();
animationCurve.AddKey(0, 1f);
animationCurve.AddKey(1, 1f);
AnimationCurveEditor.AnimationCurveEditor editor = Camera.current.GetOrAddComponent<AnimationCurveEditor.AnimationCurveEditor>().Init(animationCurve, new Rect(100, 100, 600, 300));
```

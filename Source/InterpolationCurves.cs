namespace Reroll2 {
	/**
	 * These are functions that describe the change of a value over time. See http://easings.net/ for more info.
	 */
	public static class InterpolationCurves {
		public delegate float Curve(float time, float startValue, float changeInValue, float totalDuration);

		public static Curve Linear = (t, s, c, d) => {
			t /= d;
			return c*t + s;
		};

		public static Curve QuinticEaseOut = (t, s, c, d) => {
			t /= d;
			t--;
			return c*(t*t*t*t*t + 1) + s;
		};

		public static Curve CubicEaseInOut = (t, s, c, d) => {
			if ((t /= d/2) < 1) return c/2*t*t*t + s;
			return c/2*((t -= 2)*t*t + 2) + s;
		};

		public static Curve CubicEaseOut = (t, s, c, d) => {
			return c * ((t = t / d - 1) * t * t + 1) + s;
		};
	}
}
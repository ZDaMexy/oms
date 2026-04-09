using System;

namespace oms.Input
{
    public static class OmsMouseAxisCapture
    {
        public static bool TryResolve(float deltaX, float deltaY, float threshold, out OmsMouseAxis axis, out OmsAxisDirection direction)
        {
            if (threshold <= 0)
                throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "Mouse-axis capture threshold must be positive.");

            float absoluteX = Math.Abs(deltaX);
            float absoluteY = Math.Abs(deltaY);

            if (absoluteX < threshold && absoluteY < threshold)
            {
                axis = default;
                direction = default;
                return false;
            }

            if (absoluteX >= absoluteY)
            {
                axis = OmsMouseAxis.X;
                direction = deltaX >= 0 ? OmsAxisDirection.Positive : OmsAxisDirection.Negative;
                return true;
            }

            axis = OmsMouseAxis.Y;
            direction = deltaY >= 0 ? OmsAxisDirection.Positive : OmsAxisDirection.Negative;
            return true;
        }
    }
}

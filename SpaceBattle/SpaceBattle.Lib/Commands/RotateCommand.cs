using System;
using App;

namespace SpaceBattle.Lib
{
    public class RotateCommand : ICommand
    {
        private readonly IRotatable _rotatable;

        public RotateCommand(IRotatable rotatable)
        {
            _rotatable = rotatable ?? throw new ArgumentNullException(nameof(rotatable));
        }

        public void Execute()
        {
            try
            {
                var newAngle = _rotatable.Angle + _rotatable.AngularVelocity;
                _rotatable.Angle = newAngle;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("", ex);
            }
        }
    }
}

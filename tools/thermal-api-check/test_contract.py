"""The static engine-contract check must reject changed units, dispatch and missing methods."""
import unittest
from run import verify_aero_contract


class AeroContractTests(unittest.TestCase):
    # Minimal synthetic methods exercise the parser without executing or copying a native engine.
    SOURCE = '''private void ApplyForceWorld(object force)
    {
        rigidBody.ApplyForce(1f / 60f, force.Value);
\t}
private void ApplyImplusesWorld(object force)
    {
        rigidBody.ApplyPointImpulse(force.Value, position.Value - Offset);
        rigidBody.ApplyAngularImpulse(torque.Value * (1f / 60f) * MyFakes.SIMULATION_SPEED);
\t}
private void AddForceInternal(object force)
    {
        case MyPhysicsForceType.APPLY_WORLD_FORCE:
            ApplyForceWorld(force, position, RigidBody);
            break;
\t}
'''

    def test_expected_contract_is_recognized(self):
        verify_aero_contract(self.SOURCE)

    def test_unit_dispatch_and_missing_method_changes_are_rejected(self):
        changes = [
            ("1f / 60f", "1f / 30f"),
            ("torque.Value * (1f / 60f)", "torque.Value"),
            ("ApplyPointImpulse(force.Value,", "ApplyPointImpulse(force.Value * 60f,"),
            ("ApplyForceWorld(force, position, RigidBody);", "ApplyForceWorld(force, position, torque);"),
            ("private void ApplyForceWorld", "private void Removed"),
        ]
        for old, new in changes:
            with self.subTest(old=old), self.assertRaises(RuntimeError):
                verify_aero_contract(self.SOURCE.replace(old, new))


if __name__ == "__main__":
    unittest.main()

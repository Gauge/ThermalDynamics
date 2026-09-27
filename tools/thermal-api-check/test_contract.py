"""The static engine-contract check must reject changed units, dispatch and missing methods."""
import unittest
from run import verify_aero_contract, profile_framework_bootstrap


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


class ProfileFrameworkTests(unittest.TestCase):
    SOURCE = """private static void AllowDefaultNamespaces(object handle)
    {
        handle.AllowTypes(MyWhitelistTarget.Both, typeof(object), typeof(string), typeof(bool), typeof(byte), typeof(int), typeof(long), typeof(float), typeof(double), typeof(Array), typeof(Func<, >), typeof(Func<, , , >));
        handle.AllowMembers(MyWhitelistTarget.Both, (from m in AllDeclaredMembers(typeof(Delegate))
            where m.Name != "CreateDelegate"
            select m).ToArray());
\t}
"""

    def test_permissions_come_from_game_declarations(self):
        result = profile_framework_bootstrap(self.SOURCE)
        self.assertIn("typeof(Array)", result)
        self.assertIn('GetMethod("GetInvocationList")', result)
        self.assertNotIn("CreateDelegate", result)
        self.assertNotIn("typeof(System.Reflection", result)

    def test_missing_or_changed_permissions_are_rejected(self):
        for before, after in [("typeof(Array)", "typeof(Removed)"),
                              ("typeof(Func<, >)", "typeof(Func<>)"),
                              ('m.Name != "CreateDelegate"', 'm.Name == "CreateDelegate"'),
                              ("AllowDefaultNamespaces", "Removed")]:
            with self.subTest(before=before), self.assertRaises(RuntimeError):
                profile_framework_bootstrap(self.SOURCE.replace(before, after))


if __name__ == "__main__":
    unittest.main()

using UnityEngine;

public class SimpleDamageable : MonoBehaviour
{
    public float MaxHP = 10f;
    private float _hp;

    private void Awake() { _hp = MaxHP; }

    // returns true if dead
    public bool ApplyDamage(float amount)
    {
        _hp -= amount;
        if (_hp <= 0f)
        {
            // Boom: in training we just signal and optionally disable renderer
            return true;
        }
        return false;
    }
}

using UnityEngine;

namespace ScriptablePool
{
	[RequireComponent(typeof(Rigidbody))]
	public class PoolableResetRigidbody : MonoBehaviour, IPoolableReset
	{
		void IPoolableReset.PoolReset()
		{
			TryGetComponent(out Rigidbody _rigidbody);

			_rigidbody.velocity = Vector3.zero;
			_rigidbody.angularVelocity = Vector3.zero;
		}
	}
}

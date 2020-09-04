using System.Collections.Generic;
using UnityEngine;

namespace ScriptablePool
{
	public abstract class PoolReference<T> : PoolAbstract<T> where T : Component
	{

		#region Variables

		[SerializeReference]
		private T _prefab = null;

		protected override T _Prefab => _prefab;

		#endregion

		#region EnablePool

		protected override void OnEnablePool()
		{
			for (int i = 0; i < poolOverideSize; i++)
			{
				var component = Instantiate(_prefab, poolParent);

				var poolObject = new PoolObject<T>(component);

				poolObject.gameObject.SetActive(false);

				_poolList.Add(poolObject);
				_queu.Enqueue(poolObject);

#if UNITY_EDITOR
				component.name = $"{_prefab.name}_{i:000}";
#endif
			}
		}

		#endregion

	}
}

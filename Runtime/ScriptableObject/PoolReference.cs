using System.Collections.Generic;
using UnityEngine;

namespace ScriptablePool
{
	public abstract class PoolReference<C> : PoolAbstract where C : Component
	{

		#region Variables

		[SerializeReference]
		private C _prefab;

		protected override GameObject _Prefab => _prefab.gameObject;

		#endregion

		#region EnablePool

		protected override void OnEnablePool()
		{
#if UNITY_EDITOR
			GameObject pool = new GameObject($"Pool {_prefab.name}");
			pool.SetActive(false);
			pool.isStatic = true;

			_parent = pool.transform;
			_parent.parent = _bigParent;
#endif

			GameObject go;

			for (int i = 0; i < _poolOverideSize; i++)
			{
				go = Instantiate(_prefab, _parent).gameObject;
				go.SetActive(false);

				_poolList.Add(go);
				_queu.Enqueue(go);

#if UNITY_EDITOR
				go.name = $"{_prefab.name}_{i:000}";
#endif
			}
		}

		#endregion

	}
}

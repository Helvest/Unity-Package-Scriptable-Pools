using System.Collections.Generic;
using UnityEngine;
using System;

namespace ScriptablePool
{
	public abstract class PoolAbstract : ScriptableObject
	{
		#region Variable

		[SerializeField]
		protected ushort _poolStartSize = 100;
		[SerializeField]
		protected ushort _poolMaxSize = ushort.MaxValue;
		[SerializeField]
		protected bool _returnNewEmptyGoIfMax = true;

		protected abstract GameObject _Prefab { get; }

		[NonSerialized]
		protected List<GameObject> _poolList;

		[NonSerialized]
		protected Queue<GameObject> _queu;

		[NonSerialized]
		protected ushort _poolOverideSize = 0;

#if UNITY_EDITOR
		[NonSerialized]
		protected static Transform _bigParent;

		[NonSerialized]
		protected Transform _parent;
#endif

		#endregion

		#region Awake And OnDisable

		public void Awake()
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				return;
			}
#endif

			EnablePool();
		}

		private void OnDisable()
		{
			_poolOverideSize = 0;
			_poolList = null;
			_queu = null;

			//Debug.LogError(this.name + ": OnDisable isPlaying: " + Application.isPlaying, this);
		}

		#endregion

		#region Dequeue

		public GameObject Dequeue(Vector3 position)
		{
			return _Dequeue(position, Quaternion.identity);
		}

		public T Dequeue<T>(Vector3 position) where T : MonoBehaviour
		{
			return _Dequeue<T>(position, Quaternion.identity);
		}

		public GameObject Dequeue(Transform parent = null)
		{
			return _Dequeue(Vector3.zero, Quaternion.identity, parent);
		}

		public T Dequeue<T>(Transform parent = null) where T : MonoBehaviour
		{
			return _Dequeue<T>(Vector3.zero, Quaternion.identity, parent);
		}

		public GameObject Dequeue(Vector3 position, Quaternion rotation, Transform parent = null)
		{
			return _Dequeue(position, rotation, parent);
		}

		public T Dequeue<T>(Vector3 position, Quaternion rotation, Transform parent = null) where T : MonoBehaviour
		{
			return _Dequeue<T>(position, rotation, parent);
		}

		protected GameObject _Dequeue(Vector3 position, Quaternion rotation, Transform parent = null)
		{
#if UNITY_EDITOR
			Awake();
#endif

			GameObject go = null;

			if (_queu.Count == 0)
			{
				if (_poolList.Count < _poolMaxSize)
				{
					go = Instantiate(_Prefab, position, rotation, parent);
					_poolList.Add(go);

#if UNITY_EDITOR
					go.name = $"{_Prefab.name}_{_poolList.Count:000}";
#endif
				}
				else if (_returnNewEmptyGoIfMax)
				{
#if UNITY_EDITOR
					go = new GameObject($"{_Prefab.name}_Fake");
#else
				go = new GameObject();
#endif

					Transform transform = go.transform;

					transform.SetPositionAndRotation(position, rotation);
					transform.parent = parent;
				}
			}
			else
			{
				go = _queu.Dequeue();

				Transform transform = go.transform;

				transform.SetPositionAndRotation(position, rotation);
				transform.SetParent(parent);

				go.SetActive(true);
			}

			return go;
		}

		protected T _Dequeue<T>(Vector3 position, Quaternion rotation, Transform parent = null) where T : class
		{
			if (!_Prefab.TryGetComponent(out T _))
			{
				Debug.LogError(name + " - prefab don't have the component of type " + typeof(T).Name, this);
				return null;
			}

			return _Dequeue(position, rotation, parent).GetComponent<T>();
		}

		#endregion

		#region Enqueue

		/// <summary>
		/// Pool the object if is from this pool, if is not the object is destroy
		/// </summary>
		/// <param name="poolObject">Objet to pool</param>
		public void EnqueueOrDestroy(GameObject poolObject)
		{
#if UNITY_EDITOR
			Awake();
#endif

			if (_poolList.Contains(poolObject))
			{
				if (!_queu.Contains(poolObject))
				{
					_Enqueue(poolObject);
				}
				else
				{
					Debug.LogWarning("object already free");
				}
			}
			else
			{
				Destroy(poolObject);
			}
		}

		/// <summary>
		/// Pool the object if is from this pool, if is not return a LogError
		/// </summary>
		/// <param name="poolObject">Objet to pool</param>
		public void Enqueue(GameObject poolObject)
		{
#if UNITY_EDITOR
			Awake();
#endif

			if (!_poolList.Contains(poolObject))
			{
				Debug.LogError("Try to free object to the wrong pool");
				return;
			}

			if (_queu.Contains(poolObject))
			{
				Debug.LogWarning("object already free");
				return;
			}

			_Enqueue(poolObject);
		}

		private void _Enqueue(GameObject poolObject)
		{
			if (_poolList.Count >= _poolOverideSize)
			{
				var index = _poolList.IndexOf(poolObject);

				_poolList.RemoveAt(index);
				Destroy(poolObject);

#if UNITY_EDITOR
				/*for (int i = _poolList.Count - 1; i >= index; i--)
				{
					_poolList[i].name = $"{_Prefab.name}_{i:000}";
				}*/
#endif
			}
			else
			{
				poolObject.SetActive(false);

				poolObject.transform.localScale = _Prefab.transform.localScale;

				if (poolObject.TryGetComponent(out IPoolableReset poolable))
				{
					poolable.PoolReset();
				}

#if UNITY_EDITOR
				poolObject.transform.parent = _parent;
#else
			gameObject.transform.parent = null;
#endif
				_queu.Enqueue(poolObject);
			}
		}

		#endregion

		#region OverideSize

		public void ResetOverideSize()
		{
			SetOverideSize(_poolStartSize);
		}

		public void MaxOverideSize()
		{
			SetOverideSize(_poolMaxSize);
		}

		public void SetOverideSize(ushort newSize)
		{
			if (_poolOverideSize > _poolMaxSize)
			{
				_poolOverideSize = _poolMaxSize;
			}

			if (newSize > _poolOverideSize)
			{
				byte quantityToAdd = (byte)(newSize - _poolOverideSize);

				GameObject go;

				for (int i = quantityToAdd - 1; i >= 0; i--)
				{
					go = Instantiate(_Prefab, _parent);
					_poolList.Add(go);
					_queu.Enqueue(go);
				}
			}
			else if (newSize < _poolOverideSize)
			{
				byte quantityToRemove = (byte)(_poolOverideSize - newSize);

				if (_queu.Count < quantityToRemove)
				{
					quantityToRemove = (byte)_queu.Count;
				}

				for (int i = _queu.Count - 1; i >= 0; i--)
				{
					Destroy(_queu.Dequeue());
				}

				for (int i = _poolList.Count - 1; i >= 0; i--)
				{
					if (_poolList[i] == null)
					{
						_poolList.RemoveAt(i);
					}
				}
			}

#if UNITY_EDITOR
			for (int i = _poolList.Count - 1; i >= 0; i--)
			{
				_poolList[i].name = $"{_Prefab.name}_{i:D3}";
			}
#endif

			_poolOverideSize = newSize;
		}

		#endregion

		#region EnablePool & DisablePool

		public virtual void EnablePool()
		{
			if (_poolList != null)
			{
				return;
			}

			//Debug.LogError(name + ": EnablePool", this);

			_poolList = new List<GameObject>(_poolMaxSize);
			_queu = new Queue<GameObject>(_poolMaxSize);

			_poolOverideSize = _poolStartSize;

#if UNITY_EDITOR
			if (_bigParent == null)
			{
				GameObject poolsGO = new GameObject($"Pools");
				poolsGO.isStatic = true;
				poolsGO.SetActive(false);
				_bigParent = poolsGO.transform;
			}
#endif

			OnEnablePool();
		}

		protected virtual void OnEnablePool() { }

		public virtual void DisablePool()
		{
			if (_poolList == null)
			{
				return;
			}

			SetOverideSize(0);

			_poolList = null;
			_queu = null;

#if UNITY_EDITOR
			Destroy(_parent);
#endif

			OnDisablePool();
		}

		protected virtual void OnDisablePool() { }

		#endregion

	}
}

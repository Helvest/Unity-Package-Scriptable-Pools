using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScriptablePool
{
	public abstract class PoolAbstract<T> : ScriptableObject where T : Component
	{

		#region Variable

		public bool _dontDestroyOnLoad = false;
		public ushort poolStartSize = 100;
		public ushort poolMaxSize = ushort.MaxValue;
		public ReturnValue returnValue = ReturnValue.Null;

		public enum ReturnValue
		{
			Null,
			EmptyGameObject,
			//ForceRecycle
		}

		protected abstract T _Prefab { get; }

		[NonSerialized]
		protected readonly List<PoolObject<T>> _poolList = new List<PoolObject<T>>();

		[NonSerialized]
		protected readonly Queue<PoolObject<T>> _queu = new Queue<PoolObject<T>>();

		public ushort poolOverideSize { get; protected set; } = 0;

		public Transform poolParent { get; protected set; }

#if UNITY_EDITOR
		[NonSerialized]
		protected static Transform _allPoolsParent;
		[NonSerialized]
		protected static Transform _allPoolsParentDDOL;
#endif

		#endregion

		#region Awake And OnDisable

		private void Awake()
		{
			Init();
		}

		public void Init()
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
			poolOverideSize = 0;

			_poolList.Clear();
			_poolList.TrimExcess();
			_queu.Clear();
			_queu.TrimExcess();

			//Debug.LogError(this.name + ": OnDisable isPlaying: " + Application.isPlaying, this);
		}

		#endregion

		#region PoolObject

		protected bool _TryDequeue(out PoolObject<T> poolObject, Vector3 position = new Vector3(), Quaternion rotation = new Quaternion(), Transform parent = null, bool active = true)
		{
#if UNITY_EDITOR
			Init();
#endif

			while (_queu.Count != 0)
			{
				poolObject = _queu.Dequeue();

				if (poolObject.gameObject)
				{
					Transform transform = poolObject.transform;

					transform.SetPositionAndRotation(position, rotation);
					transform.SetParent(parent);

					poolObject.gameObject.SetActive(active);

					return true;
				}
				else
				{
					_poolList.Remove(poolObject);
				}
			}

			if (_poolList.Count < poolMaxSize)
			{
				T instance = Instantiate(_Prefab, position, rotation, parent);

				poolObject = new PoolObject<T>(instance);

				_poolList.Add(poolObject);

				poolObject.gameObject.SetActive(active);

#if UNITY_EDITOR
				poolObject.gameObject.name = $"{_Prefab.name}_{(_poolList.Count - 1):000}";
#endif
				return true;
			}

			poolObject = null;
			return false;
		}

		#endregion

		#region Dequeue T

		public T Dequeue(Vector3 position = new Vector3(), Quaternion rotation = new Quaternion(), Transform parent = null, bool active = true)
		{
			TryDequeue(out var component, position, rotation, parent, active);
			return component;
		}

		public bool TryDequeue(out T component, Vector3 position = new Vector3(), Quaternion rotation = new Quaternion(), Transform parent = null, bool active = true)
		{
			if (_TryDequeue(out var poolObject, position, rotation, parent, active))
			{
				component = poolObject.component;
				return true;
			}

			component = null;
			return false;
		}

		#endregion

		#region Dequeue GameObject

		public GameObject DequeueGameObject(Vector3 position = new Vector3(), Quaternion rotation = new Quaternion(), Transform parent = null, bool active = true)
		{
			TryDequeueGameObject(out var gameObject, position, rotation, parent, active);
			return gameObject;
		}

		public bool TryDequeueGameObject(out GameObject gameObject, Vector3 position = new Vector3(), Quaternion rotation = new Quaternion(), Transform parent = null, bool active = true)
		{
			if (_TryDequeue(out var poolObject, position, rotation, parent, active))
			{
				gameObject = poolObject.gameObject;
				return true;
			}
			else if (returnValue == ReturnValue.EmptyGameObject)
			{
#if UNITY_EDITOR
				gameObject = new GameObject($"{_Prefab.name}_Fake");
#else
				gameObject = new GameObject();
#endif

				var transform = gameObject.transform;
				transform.SetPositionAndRotation(position, rotation);
				transform.parent = parent;

				return true;
			}

			gameObject = null;
			return false;
		}

		#endregion

		#region Dequeue Transform

		public Transform DequeueTransform(Vector3 position = new Vector3(), Quaternion rotation = new Quaternion(), Transform parent = null, bool active = true)
		{
			TryDequeueTransform(out var transform, position, rotation, parent, active);
			return transform;
		}

		public bool TryDequeueTransform(out Transform transform, Vector3 position = new Vector3(), Quaternion rotation = new Quaternion(), Transform parent = null, bool active = true)
		{
			if (_TryDequeue(out var poolObject, position, rotation, parent, active))
			{
				transform = poolObject.transform;
				return true;
			}
			else if (returnValue == ReturnValue.EmptyGameObject)
			{
#if UNITY_EDITOR
				transform = new GameObject($"{_Prefab.name}_Fake").transform;
#else
				transform = new GameObject().transform;
#endif
				transform.SetPositionAndRotation(position, rotation);
				transform.parent = parent;

				return true;
			}

			transform = null;
			return false;
		}

		#endregion

		#region Dequeue Component

		public C DequeueComponent<C>(Vector3 position = new Vector3(), Quaternion rotation = new Quaternion(), Transform parent = null, bool active = true) where C : Component
		{
			TryDequeueComponent(out C component, position, rotation, parent, active);
			return component;
		}

		public bool TryDequeueComponent<C>(out C component, Vector3 position = new Vector3(), Quaternion rotation = new Quaternion(), Transform parent = null, bool active = true) where C : Component
		{
			if (!_Prefab.TryGetComponent(out C _))
			{
				Debug.LogWarning(name + " - prefab don't have the component of type " + typeof(T).Name, this);

				component = null;
				return false;
			}

			if (_TryDequeue(out var poolObject, position, rotation, parent, active))
			{
				return poolObject.gameObject.TryGetComponent<C>(out component);
			}

			component = null;
			return false;
		}

		#endregion

		#region Enqueue

		/// <summary>
		/// Pool the object if is from this pool, if is not the object is destroy
		/// </summary>
		/// <param name="gameObject">Objet to pool</param>
		public void EnqueueOrDestroy(GameObject gameObject)
		{
#if UNITY_EDITOR
			Init();
#endif

			if (!gameObject.TryGetComponent(out T component))
			{
				//Debug.LogError("TryGetComponent " + typeof(T).Name);
				Destroy(gameObject);
			}

			if (!Enqueue(component))
			{
				//Debug.LogError("Enqueue Fail: " + gameObject.name);
				Destroy(gameObject);
			}
		}

		/// <summary>
		/// Pool the object if is from this pool, if is not return a LogError
		/// </summary>
		/// <param name="component">Objet to pool</param>
		public bool Enqueue(T component)
		{
#if UNITY_EDITOR
			Init();
#endif

			PoolObject<T> poolObject = null;

			foreach (var item in _poolList)
			{
				if (item.component.Equals(component))
				{
					poolObject = item;
					break;
				}
			}

			if (poolObject == null)
			{
				//Debug.LogWarning("Try to free object to the wrong pool");
				return false;
			}

			if (_queu.Contains(poolObject))
			{
				Debug.LogWarning("object already free");
				return true;
			}

			_Enqueue(poolObject);
			return true;
		}

		private void _Enqueue(PoolObject<T> poolObject)
		{
			if (_poolList.Count > poolOverideSize)
			{
				var index = _poolList.IndexOf(poolObject);

				_poolList.RemoveAt(index);

				//Debug.LogError("Destroy " + poolObject.gameObject.name);

				Destroy(poolObject.gameObject);

#if UNITY_EDITOR
				for (int i = _poolList.Count - 1; i >= index; i--)
				{
					_poolList[i].gameObject.name = $"{_Prefab.name}_{i:000}";
				}
#endif
			}
			else
			{
				poolObject.gameObject.SetActive(false);

				poolObject.transform.localScale = _Prefab.transform.localScale;

				var iPoolableResetArray = poolObject.gameObject.GetComponents<IPoolableReset>();

				foreach (var iPoolableReset in iPoolableResetArray)
				{
					iPoolableReset.PoolReset();
				}

				poolObject.transform.parent = poolParent;

				_queu.Enqueue(poolObject);
			}
		}

		#endregion

		#region OverideSize

		public void ResetOverideSize()
		{
			SetOverideSize(poolStartSize);
		}

		public void MaxOverideSize()
		{
			SetOverideSize(poolMaxSize);
		}

		public void SetOverideSize(ushort newSize)
		{
			if (poolOverideSize > poolMaxSize)
			{
				poolOverideSize = poolMaxSize;
			}

			if (newSize > poolOverideSize)
			{
				byte quantityToAdd = (byte)(newSize - poolOverideSize);

				T component;

				for (int i = quantityToAdd - 1; i >= 0; i--)
				{
					component = Instantiate(_Prefab, poolParent);

					var poolObject = new PoolObject<T>(component);

					_poolList.Add(poolObject);
					_queu.Enqueue(poolObject);
				}
			}
			else if (newSize < poolOverideSize)
			{
				byte quantityToRemove = (byte)(poolOverideSize - newSize);

				if (_queu.Count < quantityToRemove)
				{
					quantityToRemove = (byte)_queu.Count;
				}

				for (int i = _queu.Count - 1; i >= 0; i--)
				{
					Destroy(_queu.Dequeue().gameObject);
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
				_poolList[i].gameObject.name = $"{_Prefab.name}_{i:D3}";
			}
#endif

			poolOverideSize = newSize;
		}

		#endregion

		#region EnablePool

		public virtual void EnablePool()
		{
			if (poolParent != null)
			{
				return;
			}

			//Debug.LogError(name + ": EnablePool", this);

			poolOverideSize = poolStartSize;

			GameObject pool = new GameObject(name);
			pool.SetActive(false);
			pool.isStatic = true;

			if (_dontDestroyOnLoad)
			{
				DontDestroyOnLoad(pool);
			}

			poolParent = pool.transform;

#if UNITY_EDITOR
			if (_dontDestroyOnLoad)
			{
				if (_allPoolsParentDDOL == null)
				{
					var allPoolsGo = new GameObject($"Pools DDOL");
					allPoolsGo.isStatic = true;
					allPoolsGo.SetActive(false);

					DontDestroyOnLoad(allPoolsGo);

					_allPoolsParentDDOL = allPoolsGo.transform;
				}

				poolParent.parent = _allPoolsParentDDOL;
			}
			else
			{
				if (_allPoolsParent == null)
				{
					var allPoolsGo = new GameObject($"Pools");
					allPoolsGo.isStatic = true;
					allPoolsGo.SetActive(false);
					_allPoolsParent = allPoolsGo.transform;
				}

				poolParent.parent = _allPoolsParent;
			}
#endif

			OnEnablePool();
		}

		protected virtual void OnEnablePool() { }

		#endregion

		#region DisablePool

		public virtual void DisablePool()
		{
			if (poolParent == null)
			{
				return;
			}

			SetOverideSize(0);

			_poolList.Clear();
			_poolList.TrimExcess();
			_queu.Clear();
			_queu.TrimExcess();

			Destroy(poolParent);

			OnDisablePool();
		}

		protected virtual void OnDisablePool() { }

		#endregion

	}
}

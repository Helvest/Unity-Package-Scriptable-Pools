using UnityEngine;

namespace ScriptablePool
{
	public class PoolObject<T> where T : Component
	{
		public GameObject gameObject { get; private set; }

		public Transform transform { get; private set; }

		public T component { get; private set; }

		public PoolObject(T component)
		{
			this.component = component;
			gameObject = component.gameObject;
			transform = gameObject.transform;
		}
	}
}

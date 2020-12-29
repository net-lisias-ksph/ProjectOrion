using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Orion
{
    public class ObjectPool : MonoBehaviour //borrowed from BDArmory
    {
        public GameObject objectPool;
        public int amount { get { return pooledObject.Count; } }
        public int lastIndex = 0;

        List<GameObject> pooledObject;

        void Awake()
        {
            pooledObject = new List<GameObject>();
        }

        void OnDestroy()
        {
            foreach (var poolObject in pooledObject)
                if (poolObject != null)
                    Destroy(poolObject);
        }

        public GameObject GetPooledObject(int index)
        {
            return pooledObject[index];
        }

        public void AdjustSize(int count)
        {
            if (count > amount) 
                PoolObjects(count - amount);
            else
            { 
                for (int i = count; i < amount; ++i)
                {
                    if (pooledObject[i] == null) continue;
                    Destroy(pooledObject[i]);
                }
                pooledObject.RemoveRange(count, amount - count);
                lastIndex = 0;
            }
        }

        private void PoolObjects(int count)
        {
            for (int i = 0; i < count; ++i)
            {
                GameObject obj = Instantiate(objectPool);
                obj.transform.SetParent(transform);
                obj.SetActive(false);
                pooledObject.Add(obj);
            }
        }

        public GameObject GetPooledObject()
        {
            for (int i = lastIndex + 1; i < pooledObject.Count; ++i)
            {
                if (!pooledObject[i].activeInHierarchy)
                {
                    lastIndex = i;
                    DisableAfterDelay(pooledObject[i], 10);
                    return pooledObject[i];
                }
            }
            for (int i = 0; i < lastIndex + 1; ++i)
            {
                if (!pooledObject[i].activeInHierarchy)
                {
                    lastIndex = i;
                    DisableAfterDelay(pooledObject[i], 10);
                    return pooledObject[i];
                }
            }
            return null;
        }

        public void DisableAfterDelay(GameObject obj, float t)
        {
            StartCoroutine(DisableObject(obj, t));
        }

        IEnumerator DisableObject(GameObject obj, float t)
        {
            yield return new WaitForSeconds(t);
            if (obj)
            {
                obj.SetActive(false);
                obj.transform.parent = transform;
            }
        }

        public static ObjectPool CreateObjectPool(GameObject obj, int amount)
        {
            GameObject objectPool = new GameObject(obj.name + "Pool");
            ObjectPool pool = objectPool.AddComponent<ObjectPool>();
            pool.objectPool = obj;
            pool.PoolObjects(amount);

            return pool;
        }
    }
}

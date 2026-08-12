using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine
{
    public class Object
    {
        internal bool Persistent { get; set; }

        public string name { get; set; }

        public static bool operator ==(Object left, Object right)
        {
            return ReferenceEquals(left, right);
        }

        public static bool operator !=(Object left, Object right)
        {
            return !ReferenceEquals(left, right);
        }

        public override bool Equals(object value)
        {
            return ReferenceEquals(this, value);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static void DontDestroyOnLoad(Object target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            GameObject gameObject = target as GameObject;
            if (gameObject == null)
            {
                Component component = target as Component;
                gameObject = component == null ? null : component.gameObject;
            }
            if (gameObject == null)
            {
                throw new ArgumentException(
                    "Only facade game objects and components are supported.",
                    "target"
                );
            }

            FacadeRuntime.HierarchyRoot(gameObject).Persistent = true;
        }
    }

    public class Component : Object
    {
        internal Component(GameObject gameObject)
        {
            this.gameObject = gameObject;
        }

        protected Component()
        {
        }

        public GameObject gameObject { get; internal set; }

        public Transform transform
        {
            get { return gameObject == null ? null : gameObject.transform; }
        }
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; } = true;
    }

    public class MonoBehaviour : Behaviour
    {
    }

    public class Transform : Component
    {
        private readonly List<Transform> children = new List<Transform>();

        internal Transform(GameObject gameObject) : base(gameObject)
        {
        }

        public Transform parent { get; private set; }

        internal IEnumerable<Transform> Children
        {
            get { return children; }
        }

        public void SetParent(Transform value, bool worldPositionStays)
        {
            if (parent != null)
            {
                parent.children.Remove(this);
            }
            parent = value;
            if (parent != null)
            {
                parent.children.Add(this);
            }
        }
    }

    public class GameObject : Object
    {
        private readonly List<Component> components = new List<Component>();

        public GameObject(string name)
        {
            this.name = name;
            activeSelf = true;
            transform = new Transform(this);
            FacadeRuntime.Register(this);
        }

        public bool activeSelf { get; private set; }

        public Transform transform { get; private set; }

        public Component AddComponent(Type componentType)
        {
            if (componentType == null)
            {
                throw new ArgumentNullException("componentType");
            }
            if (!typeof(Component).IsAssignableFrom(componentType) ||
                componentType.IsAbstract)
            {
                throw new ArgumentException(
                    "The requested type is not a concrete component.",
                    "componentType"
                );
            }

            Component component = (Component)Activator.CreateInstance(
                componentType,
                true
            );
            component.gameObject = this;
            components.Add(component);
            return component;
        }

        internal int AttachedComponentCount
        {
            get { return components.Count; }
        }
    }

    public static class FacadeRuntime
    {
        private static readonly List<GameObject> Objects =
            new List<GameObject>();

        public static void Reset()
        {
            Objects.Clear();
        }

        public static GameObject FindRoot(string name)
        {
            return Objects.SingleOrDefault(candidate =>
                candidate.transform.parent == null &&
                string.Equals(candidate.name, name, StringComparison.Ordinal)
            );
        }

        public static int CountRoots(string name)
        {
            return Objects.Count(candidate =>
                candidate.transform.parent == null &&
                string.Equals(candidate.name, name, StringComparison.Ordinal)
            );
        }

        public static bool Contains(GameObject gameObject)
        {
            return Objects.Contains(gameObject);
        }

        public static bool IsPersistent(GameObject gameObject)
        {
            return HierarchyRoot(gameObject).Persistent;
        }

        public static GameObject ParentOf(GameObject gameObject)
        {
            Transform parent = gameObject.transform.parent;
            return parent == null ? null : parent.gameObject;
        }

        public static int AttachedComponentCount(GameObject gameObject)
        {
            return gameObject.AttachedComponentCount;
        }

        public static void LoadRepresentativeScene()
        {
            Objects.RemoveAll(candidate =>
                !HierarchyRoot(candidate).Persistent
            );
        }

        internal static void Register(GameObject gameObject)
        {
            Objects.Add(gameObject);
        }

        internal static GameObject HierarchyRoot(GameObject gameObject)
        {
            GameObject current = gameObject;
            while (current.transform.parent != null)
            {
                current = current.transform.parent.gameObject;
            }
            return current;
        }
    }
}

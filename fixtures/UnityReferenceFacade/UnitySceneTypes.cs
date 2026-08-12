namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        private readonly string name;

        internal Scene(string name)
        {
            this.name = name;
        }

        public string Name
        {
            get { return name; }
        }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(name);
        }
    }

    public static class SceneManager
    {
        private static Scene active = new Scene("FacadeScene");

        public static Scene GetActiveScene()
        {
            return active;
        }

        public static Scene CreateScene(string sceneName)
        {
            return new Scene(sceneName);
        }

        public static bool SetActiveScene(Scene scene)
        {
            if (!scene.IsValid())
            {
                return false;
            }
            active = scene;
            return true;
        }

        public static UnityEngine.AsyncOperation UnloadSceneAsync(Scene scene)
        {
            return scene.IsValid()
                ? new UnityEngine.AsyncOperation()
                : null;
        }
    }
}

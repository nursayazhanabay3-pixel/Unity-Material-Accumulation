
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class AccumulationSurface : MonoBehaviour
{
    [Header("Surface Settings")]
    [SerializeField, Min(2)] private int resolution = 100;
    [SerializeField, Min(1f)] private float size = 20f;

    // Mesh отвечает за отображение поверхности.
    private Mesh mesh;

    // Данные материала хранятся отдельно от Mesh.
    private float[] heights;
    private Vector3[] vertices;
    private int[] triangles;

    private int vertexCount;
    private float cellSize;
    private bool isInitialized;

    public float CellSize => cellSize;
    public float Size => size;

    private void Awake()
    {
        resolution = Mathf.Max(2, resolution);
        size = Mathf.Max(1f, size);

        vertexCount = resolution + 1;
        cellSize = size / resolution;

        int count = vertexCount * vertexCount;

        // Массивы создаются один раз и затем переиспользуются.
        heights = new float[count];
        vertices = new Vector3[count];
        triangles = new int[resolution * resolution * 6];

        BuildMesh();
        isInitialized = true;
    }

    private void BuildMesh()
    {
        mesh = new Mesh();
        mesh.name = "Accumulation Mesh";
        mesh.MarkDynamic();

        // Создаём вершины ровной квадратной поверхности.
        for (int z = 0; z < vertexCount; z++)
        {
            for (int x = 0; x < vertexCount; x++)
            {
                int index = z * vertexCount + x;

                vertices[index] = new Vector3(
                    x * cellSize,
                    0f,
                    z * cellSize
                );
            }
        }

        // Соединяем вершины треугольниками.
        int t = 0;

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int a = z * vertexCount + x;
                int b = a + 1;
                int c = a + vertexCount;
                int d = c + 1;

                triangles[t++] = a;
                triangles[t++] = c;
                triangles[t++] = b;

                triangles[t++] = b;
                triangles[t++] = c;
                triangles[t++] = d;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    public void AccumulateAlongPath(
        Vector3 fromWorld,
        Vector3 toWorld,
        float fromRadius,
        float toRadius,
        float deltaTime,
        float accumulationSpeed)
    {
        if (!isInitialized ||
            deltaTime <= 0f ||
            accumulationSpeed <= 0f)
        {
            return;
        }

        // Переводим позиции из мировых координат
        // в локальные координаты поверхности.
        Vector3 from = transform.InverseTransformPoint(fromWorld);
        Vector3 to = transform.InverseTransformPoint(toWorld);

        float distance = Vector2.Distance(
            new Vector2(from.x, from.z),
            new Vector2(to.x, to.z)
        );

        float radiusChange = Mathf.Abs(toRadius - fromRadius);

        // Маленький шаг помогает избежать пропусков
        // при быстром движении зоны.
        float spacing = cellSize * 0.5f;

        int steps = Mathf.Max(
            1,
            Mathf.CeilToInt(
                Mathf.Max(distance, radiusChange) / spacing
            )
        );

        int sampleCount = steps + 1;

        // Скорость не зависит от FPS.
        float addedHeight =
            accumulationSpeed * deltaTime / sampleCount;

        // Обрабатываем весь путь между предыдущей
        // и текущей позициями зоны.
        for (int step = 0; step < sampleCount; step++)
        {
            float t = (float)step / steps;

            Vector3 position = Vector3.Lerp(from, to, t);
            float radius = Mathf.Lerp(fromRadius, toRadius, t);

            AddAt(position, radius, addedHeight);
        }

        UpdateSurfaceMesh();
    }

    private void AddAt(
        Vector3 center,
        float radius,
        float addedHeight)
    {
        if (radius <= 0f)
            return;

        int minX = Mathf.Clamp(
            Mathf.FloorToInt((center.x - radius) / cellSize),
            0,
            resolution
        );

        int maxX = Mathf.Clamp(
            Mathf.CeilToInt((center.x + radius) / cellSize),
            0,
            resolution
        );

        int minZ = Mathf.Clamp(
            Mathf.FloorToInt((center.z - radius) / cellSize),
            0,
            resolution
        );

        int maxZ = Mathf.Clamp(
            Mathf.CeilToInt((center.z + radius) / cellSize),
            0,
            resolution
        );

        float radiusSquared = radius * radius;

        for (int z = minZ; z <= maxZ; z++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x * cellSize - center.x;
                float dz = z * cellSize - center.z;

                float distanceSquared = dx * dx + dz * dz;

                if (distanceSquared > radiusSquared)
                    continue;

                // Высота полусферической зоны
                // в текущей точке.
                float capHeight = Mathf.Sqrt(
                    Mathf.Max(0f, radiusSquared - distanceSquared)
                );

                int index = z * vertexCount + x;

                // Материал добавляется только там,
                // где ещё есть место до границы зоны.
                // Уже накопленная высота не уменьшается.
                if (heights[index] < capHeight)
                {
                    heights[index] = Mathf.Min(
                        heights[index] + addedHeight,
                        capHeight
                    );
                }
            }
        }
    }

    private void UpdateSurfaceMesh()
    {
        // Переносим высоты из карты данных в вершины Mesh.
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].y = heights[i];
        }

        mesh.SetVertices(vertices);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    public Vector3 ClampWorldPosition(Vector3 worldPosition)
    {
        // Не позволяем зоне выходить за границы поверхности.
        Vector3 local = transform.InverseTransformPoint(worldPosition);

        local.x = Mathf.Clamp(local.x, 0f, size);
        local.z = Mathf.Clamp(local.z, 0f, size);

        return transform.TransformPoint(local);
    }

    public void ResetSurface()
    {
        if (!isInitialized)
            return;

        // Очищаем накопленный материал.
        System.Array.Clear(heights, 0, heights.Length);

        UpdateSurfaceMesh();
    }

    private void OnDestroy()
    {
        if (mesh != null)
        {
            Destroy(mesh);
        }
    }
}
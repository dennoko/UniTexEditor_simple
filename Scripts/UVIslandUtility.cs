using UnityEngine;
using System.Collections.Generic;

namespace UniTexEditor
{
    /// <summary>
    /// UV アイランド解析ユーティリティ
    /// メッシュのUV情報からアイランドマップを生成
    /// </summary>
    public static class UVIslandUtility
    {
        /// <summary>
        /// UV アイランド情報
        /// </summary>
        public class UVIsland
        {
            public int id;
            public List<Vector2> uvPoints = new List<Vector2>();
            public Rect bounds;
        }
        
        /// <summary>
        /// メッシュからUVアイランドを抽出
        /// </summary>
        public static List<UVIsland> ExtractUVIslands(Mesh mesh)
        {
            if (mesh == null)
            {
                Debug.LogError("Mesh is null!");
                return new List<UVIsland>();
            }
            
            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;
            
            if (uvs == null || uvs.Length == 0)
            {
                Debug.LogError("Mesh has no UV data!");
                return new List<UVIsland>();
            }
            
            // 三角形ごとにグループ化（同じアイランドに属する三角形を特定）
            List<HashSet<int>> islands = new List<HashSet<int>>();
            bool[] processed = new bool[triangles.Length / 3];
            
            for (int i = 0; i < triangles.Length / 3; i++)
            {
                if (processed[i]) continue;
                
                HashSet<int> island = new HashSet<int>();
                Queue<int> queue = new Queue<int>();
                queue.Enqueue(i);
                processed[i] = true;
                
                while (queue.Count > 0)
                {
                    int triIndex = queue.Dequeue();
                    island.Add(triIndex);
                    
                    // この三角形の頂点を取得
                    int v0 = triangles[triIndex * 3];
                    int v1 = triangles[triIndex * 3 + 1];
                    int v2 = triangles[triIndex * 3 + 2];
                    
                    // 隣接する三角形を探す（UV空間で接続されているか）
                    for (int j = 0; j < triangles.Length / 3; j++)
                    {
                        if (processed[j]) continue;
                        
                        int u0 = triangles[j * 3];
                        int u1 = triangles[j * 3 + 1];
                        int u2 = triangles[j * 3 + 2];
                        
                        // UV座標で共有エッジがあるかチェック
                        if (SharesUVEdge(uvs, v0, v1, v2, u0, u1, u2))
                        {
                            queue.Enqueue(j);
                            processed[j] = true;
                        }
                    }
                }
                
                islands.Add(island);
            }
            
            // UVIslandオブジェクトに変換
            List<UVIsland> result = new List<UVIsland>();
            for (int i = 0; i < islands.Count; i++)
            {
                UVIsland island = new UVIsland { id = i };
                
                foreach (int triIndex in islands[i])
                {
                    int v0 = triangles[triIndex * 3];
                    int v1 = triangles[triIndex * 3 + 1];
                    int v2 = triangles[triIndex * 3 + 2];
                    
                    island.uvPoints.Add(uvs[v0]);
                    island.uvPoints.Add(uvs[v1]);
                    island.uvPoints.Add(uvs[v2]);
                }
                
                // バウンディングボックスを計算
                island.bounds = CalculateBounds(island.uvPoints);
                result.Add(island);
            }
            
            return result;
        }
        
        /// <summary>
        /// 2つの三角形がUV空間でエッジを共有しているかチェック
        /// </summary>
        private static bool SharesUVEdge(Vector2[] uvs, int v0, int v1, int v2, int u0, int u1, int u2)
        {
            const float epsilon = 0.0001f;
            
            Vector2[] tri1 = { uvs[v0], uvs[v1], uvs[v2] };
            Vector2[] tri2 = { uvs[u0], uvs[u1], uvs[u2] };
            
            // エッジの組み合わせをチェック
            for (int i = 0; i < 3; i++)
            {
                Vector2 edge1Start = tri1[i];
                Vector2 edge1End = tri1[(i + 1) % 3];
                
                for (int j = 0; j < 3; j++)
                {
                    Vector2 edge2Start = tri2[j];
                    Vector2 edge2End = tri2[(j + 1) % 3];
                    
                    // エッジが一致するかチェック（順方向または逆方向）
                    if ((Vector2.Distance(edge1Start, edge2Start) < epsilon && Vector2.Distance(edge1End, edge2End) < epsilon) ||
                        (Vector2.Distance(edge1Start, edge2End) < epsilon && Vector2.Distance(edge1End, edge2Start) < epsilon))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// UVポイントのバウンディングボックスを計算
        /// </summary>
        private static Rect CalculateBounds(List<Vector2> points)
        {
            if (points.Count == 0)
                return new Rect(0, 0, 0, 0);
            
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            
            foreach (Vector2 p in points)
            {
                minX = Mathf.Min(minX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxX = Mathf.Max(maxX, p.x);
                maxY = Mathf.Max(maxY, p.y);
            }
            
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
        
        /// <summary>
        /// UV アイランド ID マップを生成（テクスチャ）
        /// </summary>
        public static Texture2D GenerateIslandIDMap(List<UVIsland> islands, int width, int height)
        {
            Texture2D idMap = new Texture2D(width, height, TextureFormat.RFloat, false);
            Color[] pixels = new Color[width * height];
            
            // 初期化（-1 = アイランド無し）
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(-1, 0, 0, 0);
            }
            
            // 各アイランドをラスタライズ
            for (int i = 0; i < islands.Count; i++)
            {
                UVIsland island = islands[i];
                
                // 簡易的なラスタライズ（三角形ごと）
                for (int j = 0; j < island.uvPoints.Count; j += 3)
                {
                    if (j + 2 >= island.uvPoints.Count) break;
                    
                    Vector2 uv0 = island.uvPoints[j];
                    Vector2 uv1 = island.uvPoints[j + 1];
                    Vector2 uv2 = island.uvPoints[j + 2];
                    
                    // UV座標をピクセル座標に変換
                    Vector2Int p0 = new Vector2Int((int)(uv0.x * width), (int)(uv0.y * height));
                    Vector2Int p1 = new Vector2Int((int)(uv1.x * width), (int)(uv1.y * height));
                    Vector2Int p2 = new Vector2Int((int)(uv2.x * width), (int)(uv2.y * height));
                    
                    // 三角形のバウンディングボックスを取得
                    int minX = Mathf.Max(0, Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x)));
                    int maxX = Mathf.Min(width - 1, Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x)));
                    int minY = Mathf.Max(0, Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y)));
                    int maxY = Mathf.Min(height - 1, Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y)));
                    
                    // バウンディングボックス内のピクセルをチェック
                    for (int y = minY; y <= maxY; y++)
                    {
                        for (int x = minX; x <= maxX; x++)
                        {
                            if (IsPointInTriangle(new Vector2(x, y), p0, p1, p2))
                            {
                                pixels[y * width + x] = new Color(i, 0, 0, 1);
                            }
                        }
                    }
                }
            }
            
            idMap.SetPixels(pixels);
            idMap.Apply();
            
            return idMap;
        }
        
        /// <summary>
        /// 点が三角形内にあるかチェック（重心座標系）
        /// </summary>
        private static bool IsPointInTriangle(Vector2 p, Vector2Int a, Vector2Int b, Vector2Int c)
        {
            Vector2 av = new Vector2(a.x, a.y);
            Vector2 bv = new Vector2(b.x, b.y);
            Vector2 cv = new Vector2(c.x, c.y);
            
            float d1 = Sign(p, av, bv);
            float d2 = Sign(p, bv, cv);
            float d3 = Sign(p, cv, av);
            
            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            
            return !(hasNeg && hasPos);
        }
        
        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }
    }
}

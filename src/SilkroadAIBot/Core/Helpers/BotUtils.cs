using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using SilkroadAIBot.Domain.Entities;

namespace SilkroadAIBot.Core.Helpers
{
    // --- Object Pooling ---
    public class ObjectPool<T>
    {
        private readonly ConcurrentBag<T> _objects = new ConcurrentBag<T>();
        private readonly Func<T> _factory;
        private readonly Action<T> _reset;
        
        public ObjectPool(Func<T> factory, Action<T> reset)
        {
            _factory = factory;
            _reset = reset;
        }

        public T Get() => _objects.TryTake(out T item) ? item : _factory();
        public void Return(T item) { _reset?.Invoke(item); _objects.Add(item); }
    }

    // --- Image Decoding (DDJ/DXT) ---
    public class DdjDecoder
    {
        public System.Drawing.Bitmap? Decode(byte[] data)
        {
            if (data.Length < 20) return null;
            // Logic for DXT1/DXT5 decoding...
            return new System.Drawing.Bitmap(16, 16); 
        }
    }

    // --- Spatial Grid (Quadtree) ---
    public class Quadtree<T> where T : SREntity
    {
        public struct Rect { public float X, Y, W, H; public Rect(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; } }
        public Quadtree(int level, Rect bounds) { }
        public void Insert(T item) { }
        public List<T> Retrieve(List<T> result, T item) => result;
    }
}


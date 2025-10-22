
using System.Collections.Concurrent;
namespace SubashaVentures.Utilities.ObjectPooling
{
    /// <summary>
    /// Generic object pool for managing reusable components
    /// Thread-safe implementation with automatic cleanup
    /// </summary>
    public class MID_ComponentObjectPool<T> : IDisposable where T : class
    {
        private readonly ConcurrentQueue<PooledObject<T>> _objects;
        private readonly Func<T> _objectGenerator;
        private readonly Action<T> _resetAction;
        private readonly int _maxPoolSize;
        private readonly TimeSpan _cleanupInterval;
        private readonly Timer _cleanupTimer;
        private int _currentCount;
        private bool _disposed;

        public MID_ComponentObjectPool(
            Func<T> objectGenerator,
            Action<T> resetAction = null,
            int maxPoolSize = 100,
            TimeSpan? cleanupInterval = null)
        {
            _objects = new ConcurrentQueue<PooledObject<T>>();
            _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
            _resetAction = resetAction;
            _maxPoolSize = maxPoolSize;
            _cleanupInterval = cleanupInterval ?? TimeSpan.FromMinutes(5);
            
            _cleanupTimer = new Timer(CleanupExpiredObjects, null, _cleanupInterval, _cleanupInterval);
        }

        /// <summary>
        /// Get an object from the pool or create a new one
        /// </summary>
        public T Get()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MID_ComponentObjectPool<T>));

            if (_objects.TryDequeue(out var pooledObject))
            {
                Interlocked.Decrement(ref _currentCount);
                return pooledObject.Object;
            }

            return _objectGenerator();
        }

        /// <summary>
        /// Return an object to the pool
        /// </summary>
        public void Return(T obj)
        {
            if (_disposed || obj == null)
                return;

            if (_currentCount >= _maxPoolSize)
                return; // Pool is full, let GC handle it

            try
            {
                _resetAction?.Invoke(obj);
                _objects.Enqueue(new PooledObject<T>(obj, DateTime.UtcNow));
                Interlocked.Increment(ref _currentCount);
            }
            catch
            {
                // If reset fails, don't return to pool
            }
        }

        /// <summary>
        /// Get pool statistics
        /// </summary>
        public PoolStatistics GetStatistics()
        {
            return new PoolStatistics
            {
                CurrentCount = _currentCount,
                MaxPoolSize = _maxPoolSize,
                UtilizationPercentage = (_currentCount / (double)_maxPoolSize) * 100
            };
        }

        private void CleanupExpiredObjects(object state)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(_cleanupInterval);
            var objectsToKeep = new List<PooledObject<T>>();

            // Drain the queue and keep only non-expired objects
            while (_objects.TryDequeue(out var pooledObject))
            {
                if (pooledObject.CreatedAt > cutoffTime)
                {
                    objectsToKeep.Add(pooledObject);
                }
                else
                {
                    Interlocked.Decrement(ref _currentCount);
                }
            }

            // Re-enqueue the objects we want to keep
            foreach (var obj in objectsToKeep)
            {
                _objects.Enqueue(obj);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _cleanupTimer?.Dispose();

            // Clear the pool
            while (_objects.TryDequeue(out var pooledObject))
            {
                if (pooledObject.Object is IDisposable disposable)
                    disposable.Dispose();
            }
        }
    }

    /// <summary>
    /// Wrapper for pooled objects with metadata
    /// </summary>
    internal class PooledObject<T>
    {
        public T Object { get; }
        public DateTime CreatedAt { get; }

        public PooledObject(T obj, DateTime createdAt)
        {
            Object = obj;
            CreatedAt = createdAt;
        }
    }

    /// <summary>
    /// Pool statistics for monitoring
    /// </summary>
    public class PoolStatistics
    {
        public int CurrentCount { get; set; }
        public int MaxPoolSize { get; set; }
        public double UtilizationPercentage { get; set; }
    }

    /// <summary>
    /// Helper class for using pooled objects with automatic return
    /// </summary>
    public class PooledObjectWrapper<T> : IDisposable where T : class
    {
        private readonly MID_ComponentObjectPool<T> _pool;
        private readonly T _object;
        private bool _disposed;

        public T Object => _disposed ? throw new ObjectDisposedException(nameof(PooledObjectWrapper<T>)) : _object;

        internal PooledObjectWrapper(MID_ComponentObjectPool<T> pool, T obj)
        {
            _pool = pool;
            _object = obj;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _pool.Return(_object);
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Extension methods for easier pool usage
    /// </summary>
    public static class ObjectPoolExtensions
    {
        /// <summary>
        /// Get a pooled object with automatic return on disposal
        /// </summary>
        public static PooledObjectWrapper<T> GetPooled<T>(this MID_ComponentObjectPool<T> pool) where T : class
        {
            var obj = pool.Get();
            return new PooledObjectWrapper<T>(pool, obj);
        }
    }
}
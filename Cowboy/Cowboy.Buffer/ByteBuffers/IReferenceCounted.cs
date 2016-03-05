using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Buffer.ByteBuffers
{
    /// <summary>
    /// Reference counting interface for reusable objects
    /// </summary>
    public interface IReferenceCounted
    {
        /// <summary>
        /// Returns the reference count of this object
        /// </summary>
        int ReferenceCount { get; }

        /// <summary>
        /// Increases the reference count by 1
        /// </summary>
        IReferenceCounted Retain();

        /// <summary>
        /// Increases the reference count by <see cref="increment"/>.
        /// </summary>
        IReferenceCounted Retain(int increment);

        /// <summary>
        /// Records the current access location of this object for debugging purposes.
        /// If this object is determined to be leaked, the information recorded by this operation will be provided to you
        /// via <see cref="ResourceLeakDetector{T}"/>. This method is a shortcut to <see cref="Touch(object)"/> 
        /// with null as an argument.
        /// </summary>
        /// <returns></returns>
        IReferenceCounted Touch();

        /// <summary>
        /// Records the current access location of this object with an additional arbitrary information for debugging
        /// purposes. If this object is determined to be leaked, the information recorded by this operation will be
        /// provided to you via <see cref="ResourceLeakDetector{T}"/>.
        /// </summary>
        IReferenceCounted Touch(object hint);

        /// <summary>
        /// Decreases the reference count by 1 and deallocates this object if the reference count reaches 0.
        /// </summary>
        /// <returns>true if and only if the reference count is 0 and this object has been deallocated</returns>
        bool Release();

        /// <summary>
        /// Decreases the reference count by <see cref="decrement"/> and deallocates this object if the reference count reaches 0.
        /// </summary>
        /// <returns>true if and only if the reference count is 0 and this object has been deallocated</returns>
        bool Release(int decrement);
    }
}

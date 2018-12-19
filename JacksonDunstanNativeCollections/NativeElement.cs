using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

/// <summary>
/// NativeElement is a wrapper for a single blittable element to be passed between managed and unmanaged code in jobs
/// </summary>
/// <typeparam name="T">The blittable type to be stored</typeparam>
[StructLayout(LayoutKind.Sequential)]
[NativeContainer]
[DebuggerDisplay("{Element}")]
public unsafe struct NativeElement<T> : IDisposable where T : unmanaged
{
    // The pointer to the element
    [NativeDisableUnsafePtrRestriction]
    T* m_ptr;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    // Safety
    AtomicSafetyHandle m_Safety;

    [NativeSetClassTypeToNullOnSchedule]
    DisposeSentinel m_DisposeSentinel;
#endif

    // The allocator label
    Allocator m_AllocatorLabel;

    // Constructor which only takes a label and then calls the main constructor with an instance of T
    public NativeElement(Allocator label) : this(new T(), label) { }

    // Main constructor logic that takes an element and a label
    public NativeElement(T element, Allocator label)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        // Label check
        if (label <= Allocator.None)
            throw new ArgumentException("NativeElement must be allocated using Job, TempJob or Persistent");
#endif

        // Label set
        m_AllocatorLabel = label;

        // Allocate memory for a single T
        m_ptr = (T*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>(), 1, label);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        // Create DisposeSentinel and AtomicSafetyHandle
        DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0, label);
#endif

        // Create element to avoid unitialised data
        Element = element;
    }

    // Property for the Element stored
    public T Element
    {
        get
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Read check
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif

            // Return the element
            return *m_ptr;
        }
        set
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Write check
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif

            // Set the element
            *m_ptr = value;
        }
    }

    // Dispose of all resources
    public void Dispose()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif

        UnsafeUtility.Free(m_ptr, m_AllocatorLabel);
    }
}

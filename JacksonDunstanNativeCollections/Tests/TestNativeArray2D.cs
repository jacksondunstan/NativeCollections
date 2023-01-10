//-----------------------------------------------------------------------
// <copyright file="TestNativeChunkedList.cs" company="Jackson Dunstan">
//     Copyright (c) Jackson Dunstan. See LICENSE.md.
// </copyright>
//-----------------------------------------------------------------------

using System.Collections;
using NUnit.Framework;
using Unity.Collections;

namespace JacksonDunstan.NativeCollections.Tests
{
    /// <summary>
    /// Unit tests for <see cref="NativeArray2D{T}"/> and
    /// <see cref="NativeArray2D{T}.Enumerator"/>
    /// </summary>
    public class TestNativeArray2D
    {
        private static NativeArray2D<int> CreateArray(int width, int height)    
        {
            return new NativeArray2D<int>(width, height, Allocator.Temp);
        }
        
        [Test]
        public void ConstructorCreatesEmptyArray()
        {
            using (NativeArray2D<int> array = CreateArray(2, 3))
            {
                Assert.That(array[0, 0], Is.EqualTo(0));
                Assert.That(array[0, 1], Is.EqualTo(0));
                Assert.That(array[0, 2], Is.EqualTo(0));
                Assert.That(array[1, 0], Is.EqualTo(0));
                Assert.That(array[1, 1], Is.EqualTo(0));
                Assert.That(array[1, 2], Is.EqualTo(0));
            }
        }
        
        [Test]
        public void ConstructorThrowsForNonPositiveLength()
        {
            Assert.That(
                () => new NativeArray2D<int>(-2, 3, Allocator.Temp),
                Throws.Exception);
            Assert.That(
                () => new NativeArray2D<int>(2, -3, Allocator.Temp),
                Throws.Exception);
            Assert.That(
                () => new NativeArray2D<int>(0, 3, Allocator.Temp),
                Throws.Exception);
            Assert.That(
                () => new NativeArray2D<int>(2, 0, Allocator.Temp),
                Throws.Exception);
        }
        
        [Test]
        public void ConstructorThrowsForInvalidAllocator()
        {
            Assert.That(
                () => new NativeArray2D<int>(1, 1, Allocator.None),
                Throws.Exception);
        }
        
        [Test]
        public void ConstructorCopiesManagedArray()
        {
            int[,] managed =
            {
                {100, 200, 300},
                {400, 500, 600}
            };
            using (NativeArray2D<int> native = new NativeArray2D<int>(
                managed,
                Allocator.Temp))
            {
                Assert.That(managed[0, 0], Is.EqualTo(native[0, 0]));
                Assert.That(managed[0, 1], Is.EqualTo(native[0, 1]));
                Assert.That(managed[0, 2], Is.EqualTo(native[0, 2]));
                Assert.That(managed[1, 0], Is.EqualTo(native[1, 0]));
                Assert.That(managed[1, 1], Is.EqualTo(native[1, 1]));
                Assert.That(managed[1, 2], Is.EqualTo(native[1, 2]));
            }
        }
        
        [Test]
        public void ConstructorCopiesNativeArray()
        {
            using (NativeArray2D<int> src = CreateArray(2, 3))
            {
                NativeArray2D<int> srcAlias = src;
                srcAlias[0, 0] = 100;
                srcAlias[0, 1] = 200;
                srcAlias[0, 2] = 300;
                srcAlias[1, 0] = 400;
                srcAlias[1, 1] = 500;
                srcAlias[1, 2] = 600;
                
                using (NativeArray2D<int> dest = new NativeArray2D<int>(
                    src,
                    Allocator.Temp))
                {
                    Assert.That(dest[0, 0], Is.EqualTo(src[0, 0]));
                    Assert.That(dest[0, 1], Is.EqualTo(src[0, 1]));
                    Assert.That(dest[0, 2], Is.EqualTo(src[0, 2]));
                    Assert.That(dest[1, 0], Is.EqualTo(src[1, 0]));
                    Assert.That(dest[1, 1], Is.EqualTo(src[1, 1]));
                    Assert.That(dest[1, 2], Is.EqualTo(src[1, 2]));
                }
            }
        }
        
        [Test]
        public void LengthReturnsTotalNumberOfElements()
        {
            using (NativeArray2D<int> array = CreateArray(2, 3))
            {
                Assert.That(array.Length, Is.EqualTo(6));
            }
        }
        
        [Test]
        public void Length0ReturnsTotalNumberOfElements()
        {
            using (NativeArray2D<int> array = CreateArray(2, 3))
            {
                Assert.That(array.Length0, Is.EqualTo(2));
            }
        }
        
        [Test]
        public void Length1ReturnsTotalNumberOfElements()
        {
            using (NativeArray2D<int> array = CreateArray(2, 3))
            {
                Assert.That(array.Length1, Is.EqualTo(3));
            }
        }
        
        [Test]
        public void IndexGetsAndSetsElementAtGivenIndex()
        {
            using (NativeArray2D<int> array = CreateArray(2, 3))
            {
                NativeArray2D<int> alias = array;
                alias[0, 0] = 100;
                alias[0, 1] = 200;
                alias[0, 2] = 300;
                alias[1, 0] = 400;
                alias[1, 1] = 500;
                alias[1, 2] = 600;
                Assert.That(array[0, 0], Is.EqualTo(100));
                Assert.That(array[0, 1], Is.EqualTo(200));
                Assert.That(array[0, 2], Is.EqualTo(300));
                Assert.That(array[1, 0], Is.EqualTo(400));
                Assert.That(array[1, 1], Is.EqualTo(500));
                Assert.That(array[1, 2], Is.EqualTo(600));
            }
        }
        
        [Test]
        public void IndexOutOfBoundsThrows()
        {
            using (NativeArray2D<int> array = CreateArray(2, 3))
            {
                Assert.That(
                    () => array[-1, 0],
                    Throws.Exception);
                Assert.That(
                    () => array[2, 0],
                    Throws.Exception);
                Assert.That(
                    () => array[0, 3],
                    Throws.Exception);
                Assert.That(
                    () => array[0, -1],
                    Throws.Exception);
            }
        }
        
        [Test]
        public void IsCreatedReturnsTrueForDefaultStruct()
        {
            NativeArray2D<int> array = default(NativeArray2D<int>);
            Assert.That(array.IsCreated, Is.False);
        }
        
        [Test]
        public void IsCreatedReturnsTrueAfterConstructor()
        {
            using (NativeArray2D<int> array = CreateArray(2, 3))
            {
                Assert.That(array.IsCreated, Is.True);
            }
        }
        
        [Test]
        public void DisposeMakesArrayUnusable()
        {
            NativeArray2D<int> array = CreateArray(2, 3);
            array.Dispose();
            int val;
            Assert.That(() => val = array[0, 0], Throws.Exception);
        }
        
        [Test]
        public void CopyFromManagedArrayCopiesElements()
        {
            using (NativeArray2D<int> dest = CreateArray(2, 3))
            {
                int[,] src =
                {
                    {100, 200, 300},
                    {400, 500, 600}
                };
                
                dest.CopyFrom(src);
                
                Assert.That(src[0, 0], Is.EqualTo(dest[0, 0]));
                Assert.That(src[0, 1], Is.EqualTo(dest[0, 1]));
                Assert.That(src[0, 2], Is.EqualTo(dest[0, 2]));
                Assert.That(src[1, 0], Is.EqualTo(dest[1, 0]));
                Assert.That(src[1, 1], Is.EqualTo(dest[1, 1]));
                Assert.That(src[1, 2], Is.EqualTo(dest[1, 2]));
            }
        }
        
        [Test]
        public void CopyFromManagedArrayThrowsWhenDifferentSize()
        {
            using (NativeArray2D<int> dest = CreateArray(2, 3))
            {
                int[,] src =
                {
                    {100, 200, 300}
                };
                
                Assert.That(() => dest.CopyFrom(src), Throws.Exception);
            }
        }
        
        [Test]
        public void CopyFromNativeArrayCopiesElements()
        {
            using (NativeArray2D<int> src = CreateArray(2, 3))
            {
                using (NativeArray2D<int> dest = CreateArray(2, 3))
                {
                    NativeArray2D<int> srcAlias = src;
                    srcAlias[0, 0] = 100;
                    srcAlias[0, 1] = 200;
                    srcAlias[0, 2] = 300;
                    srcAlias[1, 0] = 400;
                    srcAlias[1, 1] = 500;
                    srcAlias[1, 2] = 600;
                    
                    dest.CopyFrom(src);
                    
                    Assert.That(dest[0, 0], Is.EqualTo(src[0, 0]));
                    Assert.That(dest[0, 1], Is.EqualTo(src[0, 1]));
                    Assert.That(dest[0, 2], Is.EqualTo(src[0, 2]));
                    Assert.That(dest[1, 0], Is.EqualTo(src[1, 0]));
                    Assert.That(dest[1, 1], Is.EqualTo(src[1, 1]));
                    Assert.That(dest[1, 2], Is.EqualTo(src[1, 2]));
                }
            }
        }
        
        [Test]
        public void CopyFromNativeArrayThrowsWhenDifferentSize()
        {
            using (NativeArray2D<int> src = CreateArray(2, 3))
            {
                using (NativeArray2D<int> dest = CreateArray(2, 4))
                {
                    Assert.That(() => dest.CopyFrom(src), Throws.Exception);
                }
            }
        }
        
        [Test]
        public void CopyToManagedArrayCopiesElements()
        {
            using (NativeArray2D<int> src = CreateArray(2, 3))
            {
                NativeArray2D<int> srcAlias = src;
                srcAlias[0, 0] = 100;
                srcAlias[0, 1] = 200;
                srcAlias[0, 2] = 300;
                srcAlias[1, 0] = 400;
                srcAlias[1, 1] = 500;
                srcAlias[1, 2] = 600;

                int[,] dest = new int[2, 3];
                
                src.CopyTo(dest);
                
                Assert.That(dest[0, 0], Is.EqualTo(src[0, 0]));
                Assert.That(dest[0, 1], Is.EqualTo(src[0, 1]));
                Assert.That(dest[0, 2], Is.EqualTo(src[0, 2]));
                Assert.That(dest[1, 0], Is.EqualTo(src[1, 0]));
                Assert.That(dest[1, 1], Is.EqualTo(src[1, 1]));
                Assert.That(dest[1, 2], Is.EqualTo(src[1, 2]));
            }
        }
        
        [Test]
        public void CopyToManagedArrayThrowsWhenDifferentSize()
        {
            using (NativeArray2D<int> src = CreateArray(2, 3))
            {
                int[,] dest = new int[2, 4];
                
                Assert.That(() => src.CopyTo(dest), Throws.Exception);
            }
        }
        
        [Test]
        public void CopyToNativeArrayCopiesElements()
        {
            using (NativeArray2D<int> src = CreateArray(2, 3))
            {
                using (NativeArray2D<int> dest = CreateArray(2, 3))
                {
                    NativeArray2D<int> srcAlias = src;
                    srcAlias[0, 0] = 100;
                    srcAlias[0, 1] = 200;
                    srcAlias[0, 2] = 300;
                    srcAlias[1, 0] = 400;
                    srcAlias[1, 1] = 500;
                    srcAlias[1, 2] = 600;

                    src.CopyTo(dest);

                    Assert.That(dest[0, 0], Is.EqualTo(src[0, 0]));
                    Assert.That(dest[0, 1], Is.EqualTo(src[0, 1]));
                    Assert.That(dest[0, 2], Is.EqualTo(src[0, 2]));
                    Assert.That(dest[1, 0], Is.EqualTo(src[1, 0]));
                    Assert.That(dest[1, 1], Is.EqualTo(src[1, 1]));
                    Assert.That(dest[1, 2], Is.EqualTo(src[1, 2]));
                }
            }
        }
        
        [Test]
        public void CopyToNativeArrayThrowsWhenDifferentSize()
        {
            using (NativeArray2D<int> src = CreateArray(2, 3))
            {
                using (NativeArray2D<int> dest = CreateArray(2, 4))
                {
                    Assert.That(() => src.CopyTo(dest), Throws.Exception);
                }
            }
        }
        
        [Test]
        public void ToArrayCreatesArrayWithSameElements()
        {
            using (NativeArray2D<int> src = CreateArray(2, 3))
            {
                NativeArray2D<int> srcAlias = src;
                srcAlias[0, 0] = 100;
                srcAlias[0, 1] = 200;
                srcAlias[0, 2] = 300;
                srcAlias[1, 0] = 400;
                srcAlias[1, 1] = 500;
                srcAlias[1, 2] = 600;

                int[,] dest = src.ToArray();
                
                Assert.That(dest[0, 0], Is.EqualTo(src[0, 0]));
                Assert.That(dest[0, 1], Is.EqualTo(src[0, 1]));
                Assert.That(dest[0, 2], Is.EqualTo(src[0, 2]));
                Assert.That(dest[1, 0], Is.EqualTo(src[1, 0]));
                Assert.That(dest[1, 1], Is.EqualTo(src[1, 1]));
                Assert.That(dest[1, 2], Is.EqualTo(src[1, 2]));
            }
        }
        
        [Test]
        public void GetEnumeratorIteratesElementsInCorrectOrder()
        {
            using (NativeArray2D<int> array = CreateArray(2, 3))
            {
                NativeArray2D<int> alias = array;
                alias[0, 0] = 100;
                alias[0, 1] = 200;
                alias[0, 2] = 300;
                alias[1, 0] = 400;
                alias[1, 1] = 500;
                alias[1, 2] = 600;

                using (NativeArray2D<int>.Enumerator e = array.GetEnumerator())
                {
                    Assert.That(e.MoveNext(), Is.True);
                    Assert.That(e.Current, Is.EqualTo(array[0, 0]));
                    Assert.That(e.MoveNext(), Is.True);
                    Assert.That(e.Current, Is.EqualTo(array[1, 0]));
                    Assert.That(e.MoveNext(), Is.True);
                    Assert.That(e.Current, Is.EqualTo(array[0, 1]));
                    Assert.That(e.MoveNext(), Is.True);
                    Assert.That(e.Current, Is.EqualTo(array[1, 1]));
                    Assert.That(e.MoveNext(), Is.True);
                    Assert.That(e.Current, Is.EqualTo(array[0, 2]));
                    Assert.That(e.MoveNext(), Is.True);
                    Assert.That(e.Current, Is.EqualTo(array[1, 2]));
                    Assert.That(e.MoveNext(), Is.False);
                }
            }
        }
        
        [Test]
        public void GetEnumeratorNonGenericIteratesElementsInCorrectOrder()
        {
            using (NativeArray2D<int> array = CreateArray(2, 3))
            {
                NativeArray2D<int> alias = array;
                alias[0, 0] = 100;
                alias[0, 1] = 200;
                alias[0, 2] = 300;
                alias[1, 0] = 400;
                alias[1, 1] = 500;
                alias[1, 2] = 600;

                IEnumerator e = ((IEnumerable)array).GetEnumerator();
                Assert.That(e.MoveNext(), Is.True);
                Assert.That(e.Current, Is.EqualTo(array[0, 0]));
                Assert.That(e.MoveNext(), Is.True);
                Assert.That(e.Current, Is.EqualTo(array[1, 0]));
                Assert.That(e.MoveNext(), Is.True);
                Assert.That(e.Current, Is.EqualTo(array[0, 1]));
                Assert.That(e.MoveNext(), Is.True);
                Assert.That(e.Current, Is.EqualTo(array[1, 1]));
                Assert.That(e.MoveNext(), Is.True);
                Assert.That(e.Current, Is.EqualTo(array[0, 2]));
                Assert.That(e.MoveNext(), Is.True);
                Assert.That(e.Current, Is.EqualTo(array[1, 2]));
                Assert.That(e.MoveNext(), Is.False);
            }
        }

        [Test]
        public void EqualsReturnsTrueOnlyForSameArray()
        {
            using (NativeArray2D<int> a1 = CreateArray(2, 3))
            {
                Assert.That(a1.Equals(a1), Is.True);
                
                using (NativeArray2D<int> a2 = CreateArray(2, 3))
                {
                    Assert.That(a1.Equals(a2), Is.False);
                }
            }
        }
        
        [Test]
        public void EqualsObjectReturnsTrueOnlyForSameArray()
        {
            using (NativeArray2D<int> a1 = CreateArray(2, 3))
            {
                Assert.That(a1.Equals((object)a1), Is.True);
                
                using (NativeArray2D<int> a2 = CreateArray(2, 3))
                {
                    Assert.That(a1.Equals((object)a2), Is.False);
                    Assert.That(a1.Equals("something else"), Is.False);
                }
            }
        }
        
        [Test]
        public void GetHashCodeReturnsUniqueValue()
        {
            using (NativeArray2D<int> a1 = CreateArray(2, 3))
            {
                using (NativeArray2D<int> a2 = CreateArray(2, 3))
                {
                    int hash1 = a1.GetHashCode();
                    int hash2 = a2.GetHashCode();
                    Assert.That(hash1, Is.Not.EqualTo(hash2));
                }
            }
        }
        
        [Test]
        public void EqualityOperatorReturnsTrueOnlyForSameArray()
        {
            using (NativeArray2D<int> a1 = CreateArray(2, 3))
            {
// Ignore warning of comparison with self
#pragma warning disable CS1718
                Assert.That(a1 == a1, Is.True);
#pragma warning restore CS1718
                
                using (NativeArray2D<int> a2 = CreateArray(2, 3))
                {
                    Assert.That(a1 == a2, Is.False);
                }
            }
        }
        
        [Test]
        public void InequalityOperatorReturnsTrueOnlyForDifferentArray()
        {
            using (NativeArray2D<int> a1 = CreateArray(2, 3))
            {
// Ignore warning of comparison with self
#pragma warning disable CS1718
                Assert.That(a1 != a1, Is.False);
#pragma warning restore CS1718
                
                using (NativeArray2D<int> a2 = CreateArray(2, 3))
                {
                    Assert.That(a1 != a2, Is.True);
                }
            }
        }
        
        [Test]
        public void EnumeratorResetReturnsToFirstElement()
        {
            using (NativeArray2D<int> array = CreateArray(2, 3))
            {
                NativeArray2D<int> alias = array;
                alias[0, 0] = 123;
                NativeArray2D<int>.Enumerator e = array.GetEnumerator();
                e.MoveNext();
                
                e.Reset();

                Assert.That(e.MoveNext(), Is.True);
                Assert.That(e.Current, Is.EqualTo(123));
            }
        }
        
        [Test]
        public void EnumeratorCurrentReturnsCurrentElementAsObject()
        {
            using (NativeArray2D<int> array = CreateArray(2, 3))
            {
                NativeArray2D<int> alias = array;
                alias[0, 0] = 123;
                NativeArray2D<int>.Enumerator e = array.GetEnumerator();
                e.MoveNext();
                
                Assert.That(((IEnumerator)e).Current, Is.EqualTo(123));
            }
        }
    }
}
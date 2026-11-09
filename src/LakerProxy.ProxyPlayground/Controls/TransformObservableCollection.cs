using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace LakerProxy.ProxyPlayground.Controls
{
  public class TransformObservableCollection<TOutput, TInput> :
    INotifyCollectionChanged, 
    IList, 
    IReadOnlyList<TOutput>, 
    IDisposable
  {
    private ObservableCollection<TInput> _wrappedCollection;
    private readonly ObservableCollection<TOutput> _transformedCollection;
    private readonly Func<TInput, TOutput> _transformFunctionFunction;
    
    public TransformObservableCollection(ObservableCollection<TInput> wrappedCollection, Func<TInput, TOutput> transformFunction)
    {
      _wrappedCollection = wrappedCollection;
      _transformFunctionFunction = transformFunction;
      ((INotifyCollectionChanged)_wrappedCollection).CollectionChanged += TransformObservableCollectionCollectionChanged;

      _transformedCollection = new ObservableCollection<TOutput>(_wrappedCollection.Select(_transformFunctionFunction));
    }

    public void Dispose()
    {
      if (_wrappedCollection == null) 
        return;
      ((INotifyCollectionChanged)_wrappedCollection).CollectionChanged -= TransformObservableCollectionCollectionChanged;
      _wrappedCollection = null;
    }

    void TransformObservableCollectionCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
      switch (e.Action)
      {
        case NotifyCollectionChangedAction.Add:
          if (e.NewItems == null || e.NewItems.Count != 1)
            break;
          _transformedCollection.Insert(e.NewStartingIndex, _transformFunctionFunction((TInput)e.NewItems[0]));
          return;
        case NotifyCollectionChangedAction.Move:
          if (e.NewItems == null || e.NewItems.Count != 1 || e.OldItems == null || e.OldItems.Count != 1)
            break;
          _transformedCollection.Move(e.OldStartingIndex, e.NewStartingIndex);
          return;
        case NotifyCollectionChangedAction.Remove:
          if (e.OldItems == null || e.OldItems.Count != 1)
            break;
          _transformedCollection.RemoveAt(e.OldStartingIndex);
          return;
        case NotifyCollectionChangedAction.Replace:
          if (e.NewItems == null || e.NewItems.Count != 1 || e.OldItems == null || e.OldItems.Count != 1 || e.OldStartingIndex != e.NewStartingIndex)
            break;
          _transformedCollection[e.OldStartingIndex] = _transformFunctionFunction((TInput)e.NewItems[0]);
          return;
      } 
      // This  is most likely called on a Clear(), we don't optimize the other cases (yet)
      _transformedCollection.Clear();
      foreach (var item in _wrappedCollection)
        _transformedCollection.Add(_transformFunctionFunction(item));
    }

    #region IList Edit functions that are unsupported because this collection is read only
    public int Add(object value) { throw new InvalidOperationException(); }
    public void Clear() { throw new InvalidOperationException(); }
    public void Insert(int index, object value) { throw new InvalidOperationException(); }
    public void Remove(object value) { throw new InvalidOperationException(); }
    public void RemoveAt(int index) { throw new InvalidOperationException(); }
    #endregion IList Edit functions that are unsupported because this collection is read only

    #region Accessors
    public TOutput this[int index] { get { return _transformedCollection[index]; } }
    object IList.this[int index] { get { return _transformedCollection[index]; } set { throw new InvalidOperationException(); } }
    public bool Contains(TOutput value) { return _transformedCollection.Contains(value); }
    bool IList.Contains(object value) { return ((IList)_transformedCollection).Contains(value); }
    public int IndexOf(TOutput value) { return _transformedCollection.IndexOf(value); }
    int IList.IndexOf(object value) { return ((IList)_transformedCollection).IndexOf(value); }
    public int Count { get { return _transformedCollection.Count; } }
    public IEnumerator<TOutput> GetEnumerator() { return _transformedCollection.GetEnumerator(); }
    IEnumerator IEnumerable.GetEnumerator() { return ((IEnumerable)_transformedCollection).GetEnumerator(); }
    #endregion Accessors

    public bool IsFixedSize { get { return false; } }
    public bool IsReadOnly { get { return true; } }
    public void CopyTo(Array array, int index) { ((IList)_transformedCollection).CopyTo(array, index); }
    public void CopyTo(TOutput[] array, int index) { _transformedCollection.CopyTo(array, index); }
    public bool IsSynchronized { get { return false; } }
    public object SyncRoot { get { return _transformedCollection; } }

    event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged
    {
      add { ((INotifyCollectionChanged)_transformedCollection).CollectionChanged += value; }
      remove { ((INotifyCollectionChanged)_transformedCollection).CollectionChanged -= value; }
    }
  }
}

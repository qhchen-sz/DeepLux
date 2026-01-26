using ICSharpCode.WpfDesign.Designer.PropertyGrid;
using ICSharpCode.WpfDesign.Designer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp1
{
    public class MyToolbox
    {
        public ObservableCollection<MyFooNode> MyFooNodes { get; private set; }

        public MyToolbox()
        {
            MyFooNodes = new ObservableCollection<MyFooNode>();
            MyFooNodes.Add(new MyFooNode() { FooType = typeof(Label), Name = "Label" });
            
    }
        public static MyToolbox Instance = new MyToolbox();
    }
    public class MyFooNode
    {
        public Type FooType { get; set; }
        private string _name;

        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
            }
        }
        public override string ToString()
        {
            return Name;
        }
    }
    static class ExtensionMethods
    {
        public static IEnumerable<string> Paths(this IDataObject data)
        {
            string[] paths = (string[])data.GetData(DataFormats.FileDrop);
            if (paths != null)
            {
                foreach (var path in paths)
                {
                    yield return path;
                }
            }
        }

        public static T GetObject<T>(this IDataObject data)
        {
            return (T)data.GetData(typeof(T).FullName);
        }

        public static Stream ToStream(this string s)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(s));
        }

        public static void AddRange<T>(this ObservableCollection<T> col, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                col.Add(item);
            }
        }

        public static void KeepSyncronizedWith<S>(this IList target, ObservableCollection<S> source, Func<S, object> convert)
        {
            target.Clear();
            foreach (var item in source)
            {
                target.Add(convert(item));
            }

            source.CollectionChanged += delegate (object sender, NotifyCollectionChangedEventArgs e) {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        target.Add(convert((S)e.NewItems[0]));
                        break;

                    case NotifyCollectionChangedAction.Remove:
                        target.RemoveAt(e.OldStartingIndex);
                        break;

                    case NotifyCollectionChangedAction.Move:
                        target.RemoveAt(e.OldStartingIndex);
                        target.Insert(e.NewStartingIndex, e.NewItems[0]);
                        break;

                    case NotifyCollectionChangedAction.Replace:
                        target[e.NewStartingIndex] = convert((S)e.NewItems[0]);
                        break;

                    case NotifyCollectionChangedAction.Reset:
                        target.Clear();
                        break;
                }
            };
        }

        public static object GetDataContext(this RoutedEventArgs e)
        {
            var f = e.OriginalSource as FrameworkElement;
            if (f != null) return f.DataContext;
            return null;
        }
    }
    public class MyDesignerModel
    {
        public MyDesignerModel()
        {
            this.m_designSurface = new DesignSurface();
        }
        static MyDesignerModel myDesignerModel;
        public static MyDesignerModel Instance
        {
            get { return myDesignerModel ?? (myDesignerModel = new MyDesignerModel()); }
            set { myDesignerModel = value; }
        }

        private DesignSurface m_designSurface;


        public DesignSurface DesignSurface
        {
            get { return this.m_designSurface; }
        }

        public IPropertyGrid PropertyGrid { get; set; }
    }
}

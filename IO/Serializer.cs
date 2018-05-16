﻿using System.IO;
using System.Runtime.Serialization;

namespace Commons.IO
{
    public class Serializer<T>
    {
        private readonly DataContractSerializer serializer;

        public Serializer()
        {
            serializer = new DataContractSerializer(typeof(T));
        }

        public T Load(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                return (T) serializer.ReadObject(stream);
            }
        }

        public void Store(T obj, string path)
        {
            using (var stream = File.Create(path))
            {
                serializer.WriteObject(stream, obj);
            }
        }
    }
}
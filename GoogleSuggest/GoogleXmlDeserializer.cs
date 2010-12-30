﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Globalization;
using RestSharp;
using RestSharp.Deserializers;
using RestSharp.Extensions;

namespace GoogleSuggest
{
    #region License
    //   Copyright 2010 John Sheehan
    //
    //   Licensed under the Apache License, Version 2.0 (the "License");
    //   you may not use this file except in compliance with the License.
    //   You may obtain a copy of the License at
    //
    //     http://www.apache.org/licenses/LICENSE-2.0
    //
    //   Unless required by applicable law or agreed to in writing, software
    //   distributed under the License is distributed on an "AS IS" BASIS,
    //   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
    //   See the License for the specific language governing permissions and
    //   limitations under the License. 
    #endregion



    public class GoogleXmlDeserializer : IDeserializer
    {
        public string RootElement { get; set; }
        public string Namespace { get; set; }
        public string DateFormat { get; set; }

        public T Deserialize<T>(RestResponse response) where T : new()
        {
            if (response.Content == null)
                return default(T);

            var doc = XDocument.Parse(response.Content);
            var root = doc.Root;
            if (RootElement.HasValue() && doc.Root != null)
            {
                root = doc.Root.Element(RootElement.AsNamespaced(Namespace));
            }

            // autodetect xml namespace
            if (!Namespace.HasValue())
            {
                RemoveNamespace(doc);
            }

            var x = new T();
            var objType = x.GetType();

            if (objType.IsSubclassOfRawGeneric(typeof(List<>)))
            {
                x = (T)HandleListDerivative(x, root, objType.Name, objType);
            }
            else
            {
                Map(x, root);
            }

            return x;
        }

        void RemoveNamespace(XDocument xdoc)
        {
            foreach (XElement e in xdoc.Root.DescendantsAndSelf())
            {
                if (e.Name.Namespace != XNamespace.None)
                {
                    e.Name = XNamespace.None.GetName(e.Name.LocalName);
                }
                if (e.Attributes().Any(a => a.IsNamespaceDeclaration || a.Name.Namespace != XNamespace.None))
                {
                    e.ReplaceAttributes(e.Attributes().Select(a => a.IsNamespaceDeclaration ? null : a.Name.Namespace != XNamespace.None ? new XAttribute(XNamespace.None.GetName(a.Name.LocalName), a.Value) : a));
                }
            }
        }

        private void Map(object x, XElement root)
        {
            var objType = x.GetType();
            var props = objType.GetProperties();

            foreach (var prop in props)
            {
                var type = prop.PropertyType;

                if (!type.IsPublic || !prop.CanWrite)
                    continue;

                var name = prop.Name.AsNamespaced(Namespace);
                var value = GetValueFromXml(root, name);

                if (value == null)
                {
                    // special case for inline list items
                    if (type.IsGenericType)
                    {
                        var genericType = type.GetGenericArguments()[0];

                        var first = GetElementByName(root, genericType.Name);
                        if (first != null)
                        {
                            var elements = root.Elements(first.Name);

                            var list = (IList)Activator.CreateInstance(type);
                            PopulateListFromElements(genericType, elements, list);
                            prop.SetValue(x, list, null);

                        }
                    }
                    continue;
                }

                // check for nullable and extract underlying type
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    type = type.GetGenericArguments()[0];
                }

                if (type.IsPrimitive)
                {
                    prop.SetValue(x, value.ChangeType(type), null);
                }
                else if (type.IsEnum)
                {
                    var converted = Enum.Parse(type, value.ToString(), false);
                    prop.SetValue(x, converted, null);
                }
                else if (type == typeof(Uri))
                {
                    var uri = new Uri(value.ToString(), UriKind.RelativeOrAbsolute);
                    prop.SetValue(x, uri, null);
                }
                else if (type == typeof(string))
                {
                    prop.SetValue(x, value, null);
                }
                else if (type == typeof(DateTime))
                {
                    if (DateFormat.HasValue())
                    {
                        value = DateTime.ParseExact(value.ToString(), DateFormat, CultureInfo.CurrentCulture);
                    }
                    else
                    {
                        value = DateTime.Parse(value.ToString());
                    }

                    prop.SetValue(x, value, null);
                }
                else if (type == typeof(Decimal))
                {
                    value = Decimal.Parse(value.ToString());
                    prop.SetValue(x, value, null);
                }
                else if (type == typeof(Guid))
                {
                    var raw = value.ToString();
                    value = string.IsNullOrEmpty(raw) ? Guid.Empty : new Guid(value.ToString());
                    prop.SetValue(x, value, null);
                }
                else if (type.IsGenericType)
                {
                    var t = type.GetGenericArguments()[0];
                    var list = (IList)Activator.CreateInstance(type);

                    var container = GetElementByName(root, prop.Name.AsNamespaced(Namespace));
                    var first = container.Elements().FirstOrDefault();

                    var elements = container.Elements(first.Name);
                    PopulateListFromElements(t, elements, list);

                    prop.SetValue(x, list, null);
                }
                else if (type.IsSubclassOfRawGeneric(typeof(List<>)))
                {
                    // handles classes that derive from List<T>
                    // e.g. a collection that also has attributes
                    var list = HandleListDerivative(x, root, prop.Name, type);
                    prop.SetValue(x, list, null);
                }
                else
                {
                    // nested property classes
                    if (root != null)
                    {
                        var element = GetElementByName(root, name);
                        if (element != null)
                        {
                            var item = CreateAndMap(type, element);
                            prop.SetValue(x, item, null);
                        }
                    }
                }
            }
        }

        private void PopulateListFromElements(Type t, IEnumerable<XElement> elements, IList list)
        {
            foreach (var element in elements)
            {
                var item = CreateAndMap(t, element);
                list.Add(item);
            }
        }

        private object HandleListDerivative(object x, XElement root, string propName, Type type)
        {
            Type t;
            Type[] genericArgs = type.GetGenericArguments();

            if (type.IsGenericType)
            {
                t = type.GetGenericArguments()[0];
            }
            else
            {
                t = type.BaseType.GetGenericArguments()[0];
            }


            var list = (IList)Activator.CreateInstance(type);

            var elements = root.Descendants(t.Name.AsNamespaced(Namespace));

            PopulateListFromElements(t, elements, list);

            // get properties too, not just list items
            // only if this isn't a generic type
            if (!type.IsGenericType)
            {
                Map(list, root.Element(propName.AsNamespaced(Namespace)));
            }

            return list;
        }

        private object CreateAndMap(Type t, XElement element)
        {
            var item = Activator.CreateInstance(t);
            Map(item, element);
            return item;
        }

        private object GetValueFromXml(XElement root, XName name)
        {
            object val = null;

            if (root != null)
            {
                var element = GetElementByName(root, name);
                if (element == null)
                {
                    var attribute = GetAttributeByName(root, name);
                    if (attribute != null)
                    {
                        val = attribute.Value;
                    }
                }
                else
                {
                    if (!element.IsEmpty || element.HasElements || element.HasAttributes)
                    {
                        val = element.Value == string.Empty ? element.FirstAttribute.Value : element.Value;
                    }
                }
            }

            return val;
        }

        private XElement GetElementByName(XElement root, XName name)
        {
            var lowerName = XName.Get(name.LocalName.ToLower(), name.NamespaceName);
            var camelName = XName.Get(name.LocalName.ToCamelCase(), name.NamespaceName);

            if (root.Element(name) != null)
            {
                return root.Element(name);
            }

            if (root.Element(lowerName) != null)
            {
                return root.Element(lowerName);
            }

            if (root.Element(camelName) != null)
            {
                return root.Element(camelName);
            }

            if (name == "Value" && root.Value != null)
            {
                return root;
            }

            // try looking for element that matches sanitized property name (Order by depth)
            var element = root.Descendants()
                              .OrderBy(d => d.Ancestors().Count())
                              .FirstOrDefault(d => d.Name.LocalName.RemoveUnderscoresAndDashes() == name.LocalName)
                              ?? root.Descendants()
                              .OrderBy(d => d.Ancestors().Count())
                              .FirstOrDefault(d => d.Name.LocalName.RemoveUnderscoresAndDashes() == name.LocalName.ToLower());

            if (element != null)
            {
                return element;
            }

            return null;
        }

        private XAttribute GetAttributeByName(XElement root, XName name)
        {
            var lowerName = XName.Get(name.LocalName.ToLower(), name.NamespaceName);
            var camelName = XName.Get(name.LocalName.ToCamelCase(), name.NamespaceName);

            if (root.Attribute(name) != null)
            {
                return root.Attribute(name);
            }

            if (root.Attribute(lowerName) != null)
            {
                return root.Attribute(lowerName);
            }

            if (root.Attribute(camelName) != null)
            {
                return root.Attribute(camelName);
            }

            // try looking for element that matches sanitized property name
            var element = root.Attributes().FirstOrDefault(d => d.Name.LocalName.RemoveUnderscoresAndDashes() == name.LocalName);
            if (element != null)
            {
                return element;
            }

            return null;
        }

    }

}


﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using SwaggerWcf.Attributes;
using SwaggerWcf.Models;

namespace SwaggerWcf.Support
{
    internal static class TypeFieldsProcessor
    {

        public static void ProcessFields(Type definitionType, DefinitionSchema schema, IList<string> hiddenTags,
                                              Stack<Type> typesStack)
        {
            var properties = definitionType.GetFields();

            foreach (var fieldInfo in properties)
            {
                DefinitionProperty prop = ProcessField(fieldInfo, hiddenTags, typesStack);

                if (prop == null)
                    continue;

                if (prop.TypeFormat.Type == ParameterType.Array)
                {
                    Type propType = fieldInfo.FieldType;

                    Type t = propType.GetElementType() ?? DefinitionsBuilder.GetEnumerableType(propType);

                    if (t != null)
                    {
                        //prop.TypeFormat = new TypeFormat(prop.TypeFormat.Type, HttpUtility.HtmlEncode(t.FullName));
                        prop.TypeFormat = new TypeFormat(prop.TypeFormat.Type, null);

                        TypeFormat st = Helpers.MapSwaggerType(t);
                        if (st.Type == ParameterType.Array || st.Type == ParameterType.Object)
                        {
                            prop.Items.TypeFormat = new TypeFormat(ParameterType.Unknown, null);
                            prop.Items.Ref = t.GetModelName();
                        }
                        else
                        {
                            prop.Items.TypeFormat = st;
                        }
                    }
                }

                if (prop.Required)
                {
                    if (schema.Required == null)
                        schema.Required = new List<string>();

                    schema.Required.Add(prop.Title);
                }
                schema.Properties.Add(prop);
            }
        }

        private static DefinitionProperty ProcessField(FieldInfo propertyInfo, IList<string> hiddenTags,
                                                          Stack<Type> typesStack)
        {
            if (propertyInfo.GetCustomAttribute<SwaggerWcfHiddenAttribute>() != null
                || propertyInfo.GetCustomAttributes<SwaggerWcfTagAttribute>()
                               .Select(t => t.TagName)
                               .Any(hiddenTags.Contains))
                return null;

            TypeFormat typeFormat = Helpers.MapSwaggerType(propertyInfo.FieldType, null);

            DefinitionProperty prop = new DefinitionProperty { Title = propertyInfo.Name };

            DataMemberAttribute dataMemberAttribute = propertyInfo.GetCustomAttribute<DataMemberAttribute>();
            var useDataMember = SwaggerWcfEndpoint.NotUseDataMemberAttributeExceptedType.Contains(propertyInfo.ReflectedType) || !SwaggerWcfEndpoint.NotUseDataMemberAttribute;
            if (useDataMember && dataMemberAttribute != null)
            {
                if (!string.IsNullOrEmpty(dataMemberAttribute.Name))
                    prop.Title = dataMemberAttribute.Name;

                prop.Required = dataMemberAttribute.IsRequired;
            }

            // Special case - if it came out required, but we unwrapped a null-able type,
            // then it's necessarily not required.  Ideally this would only set the default,
            // but we can't tell the difference between an explicit declaration of
            // IsRequired =false on the DataMember attribute and no declaration at all.
            if (prop.Required && propertyInfo.FieldType.IsGenericType &&
                propertyInfo.FieldType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                prop.Required = false;
            }

            DescriptionAttribute descriptionAttribute = propertyInfo.GetCustomAttribute<DescriptionAttribute>();
            if (descriptionAttribute != null)
                prop.Description = descriptionAttribute.Description;

            SwaggerWcfRegexAttribute regexAttr = propertyInfo.GetCustomAttribute<SwaggerWcfRegexAttribute>();
            if (regexAttr != null)
                prop.Pattern = regexAttr.Regex;

            prop.TypeFormat = typeFormat;

            if (prop.TypeFormat.Type == ParameterType.Object)
            {
                typesStack.Push(propertyInfo.FieldType);

                prop.Ref = propertyInfo.FieldType.GetModelName();

                return prop;
            }

            if (prop.TypeFormat.Type == ParameterType.Array)
            {
                Type subType = DefinitionsBuilder.GetEnumerableType(propertyInfo.FieldType);
                if (subType != null)
                {
                    TypeFormat subTypeFormat = Helpers.MapSwaggerType(subType, null);

                    if (subTypeFormat.Type == ParameterType.Object)
                        typesStack.Push(subType);

                    prop.Items = new ParameterItems
                    {
                        TypeFormat = subTypeFormat
                    };
                }
            }

            if (prop.TypeFormat.Format == "enum")
            {
                prop.Enum = new Dictionary<string, int>();

                Type propType = propertyInfo.FieldType;

                if (propType.IsGenericType && (propType.GetGenericTypeDefinition() == typeof(Nullable<>) || propType.GetGenericTypeDefinition() == typeof(List<>)))
                    propType = propType.GetEnumerableType();

                List<string> listOfEnumNames = propType.GetEnumNames().ToList();
                foreach (string enumName in listOfEnumNames)
                {
                    var enumMemberItem = Enum.Parse(propType, enumName, true);
                    int enumMemberValue = DefinitionsBuilder.GetEnumMemberValue(propType, enumName);
                    prop.Enum.Add(enumName, enumMemberValue);
                }
            }

            // Apply any options set in a [SwaggerWcfProperty]
            DefinitionsBuilder.ApplyAttributeOptions(propertyInfo, prop);

            return prop;
        }

    }
}

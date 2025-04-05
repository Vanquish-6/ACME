using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ACME.Converters
{
    public class KeyValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return string.Empty;

            var item = value;
            
            // Handle KeyValuePair<,> types
            var type = item.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var keyProp = type.GetProperty("Key");
                var valueProp = type.GetProperty("Value");
                
                if (keyProp != null && valueProp != null)
                {
                    var key = keyProp.GetValue(item);
                    var val = valueProp.GetValue(item);
                    
                    if (val != null)
                    {
                        // Try to get a Name property from the value if it exists
                        var nameProperty = val.GetType().GetProperty("Name");
                        if (nameProperty != null)
                        {
                            var name = nameProperty.GetValue(val) as string;
                            if (!string.IsNullOrEmpty(name))
                                return $"{key} - {name}";
                        }
                    }
                    
                    return $"{key} - {val}";
                }
            }
            
            // Default to ToString
            return item.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
} 
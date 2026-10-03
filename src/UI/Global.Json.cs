using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Media;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

using Innovative.SolarCalculator;

using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

using NAudio.Wave;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using static AITool.AITOOL;

namespace AITool
{
    public static partial class Global
    {

        //this may speed up json serialization
        public static readonly DefaultContractResolver JSONContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy
            {
                ProcessDictionaryKeys = false,
                OverrideSpecifiedNames = false,
                ProcessExtensionDataNames = false
            }

            //Global.JSONContractResolver.NamingStrategy = new CamelCaseNamingStrategy();
        };
        public static readonly JsonSerializerSettings JSONSettingsPretty = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.All,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            ContractResolver = JSONContractResolver,
            //NullValueHandling = NullValueHandling.Ignore,
            //DefaultValueHandling = DefaultValueHandling.Ignore,
            //ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            // Add other settings that may improve performance for your specific case
        };

        public static readonly JsonSerializerSettings JSONSettingsPerformance = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            TypeNameHandling = TypeNameHandling.Auto,
            PreserveReferencesHandling = PreserveReferencesHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ConstructorHandling = ConstructorHandling.Default,
            ObjectCreationHandling = ObjectCreationHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore,
            MetadataPropertyHandling = MetadataPropertyHandling.Default,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            //NullValueHandling = NullValueHandling.Ignore,
            //DefaultValueHandling = DefaultValueHandling.Ignore,
            //ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            // Add other settings that may improve performance for your specific case
        };

        /// <summary>
        /// Perform a deep Copy of the object, using Json as a serialisation method.
        /// </summary>
        /// <typeparam name="T">The type of object being copied.</typeparam>
        /// <param name="source">The object instance to copy.</param>
        /// <returns>The copied object.</returns>
        public static T CloneJson<T>(this T source)
        {
            // Don't serialize a null object, simply return the default for that object
            if (Object.ReferenceEquals(source, null))
            {
                return default(T);
            }

            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(source, JSONSettingsPerformance));

        }




        public static void SerializeJsonIntoStream(object value, Stream stream)
        {
            using (var sw = new StreamWriter(stream, new UTF8Encoding(false), 32768, true))
            using (var jtw = new JsonTextWriter(sw) { Formatting = Formatting.None })
            {
                var js = new JsonSerializer();
                js.Serialize(jtw, value);
                jtw.Flush();
            }
        }

        /// <summary>
        /// Writes the given object instance to a Json file.
        /// <para>Object type must have a parameterless constructor.</para>
        /// <para>Only Public properties and variables will be written to the file. These can be any type though, even other classes.</para>
        /// <para>If there are public properties/variables that you do not want written to the file, decorate them with the [JsonIgnore] attribute.</para>
        /// </summary>
        /// <typeparam name="T">The type of object being written to the file.</typeparam>
        /// <param name="filePath">The file path to write the object instance to.</param>
        /// <param name="objectToWrite">The object instance to write to the file.</param>
        /// <param name="append">If false the file will be overwritten if it already exists. If true the contents will be appended to the file.</param>
        public static string WriteToJsonFile<T>(string filePath, T objectToWrite, bool append = false) where T : new()
        {
            string Ret = "";
            TextWriter writer = null;
            try
            {

                Ret = JsonConvert.SerializeObject(objectToWrite, JSONSettingsPretty);
                if (JSONSettingsPretty.Error == null)
                {
                    if (Directory.Exists(Path.GetDirectoryName(filePath)))
                    {
                        writer = new StreamWriter(filePath, append);
                        writer.Write(Ret);
                    }
                }
                else
                {
                    Ret = "";
                    Log($"Error: While writing '{filePath}', got: " + JSONSettingsPretty.Error.ToString());
                }
            }
            catch (Exception ex)
            {
                Ret = "";
                Log($"Error: While writing '{filePath}', got: " + ex.Msg());
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
            return Ret;

        }

        /// <summary>
        /// Reads an object instance from an Json file.
        /// <para>Object type must have a parameterless constructor.</para>
        /// </summary>
        /// <typeparam name="T">The type of object to read from the file.</typeparam>
        /// <param name="filePath">The file path to read the object instance from.</param>
        /// <returns>Returns a new instance of the object read from the Json file.</returns>
        public static T ReadFromJsonFile<T>(string filePath) where T : new()
        {


            T Ret = default(T);

            TextReader reader = null;
            try
            {
                reader = new StreamReader(filePath);
                var fileContents = reader.ReadToEnd();

                JsonSerializerSettings jset = new JsonSerializerSettings { };
                jset.TypeNameHandling = TypeNameHandling.All;
                jset.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
                jset.ContractResolver = Global.JSONContractResolver;

                Ret = JsonConvert.DeserializeObject<T>(fileContents, jset);
            }
            catch (Exception ex)
            {
                Log($"Error: While reading '{filePath}', got: " + ex.Msg());
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }

            return Ret;

        }

        public static string GetJSONString(object cls2, Newtonsoft.Json.Formatting formatting = Formatting.Indented,
                                                        Newtonsoft.Json.TypeNameHandling handling = TypeNameHandling.All,
                                                        Newtonsoft.Json.PreserveReferencesHandling reference = PreserveReferencesHandling.Objects)
        {

            string Ret = "";
            try
            {

                JSONSettingsPretty.Formatting = formatting;
                JSONSettingsPretty.TypeNameHandling = handling;
                JSONSettingsPretty.PreserveReferencesHandling = reference;
                JSONSettingsPretty.Error = null;

                string contents2 = JsonConvert.SerializeObject(cls2, formatting, JSONSettingsPretty);

                if (JSONSettingsPretty.Error == null)
                {
                    Ret = contents2;
                }
                else
                {
                    Log($"Error: " + JSONSettingsPretty.Error.ToString());
                }

            }
            catch (Exception ex)
            {
                Log($"Error: " + ex.Msg());
            }
            finally
            {
            }

            return Ret;

        }

        public static T SetJSONString<T>(string JSONString) where T : new()
        {


            T Ret = default(T);

            try
            {
                //JsonSerializerSettings jset = new JsonSerializerSettings { };
                //jset.TypeNameHandling = TypeNameHandling.All;
                //jset.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
                //jset.ContractResolver = Global.JSONContractResolver;

                Ret = JsonConvert.DeserializeObject<T>(JSONString, JSONSettingsPretty);
            }
            catch (Exception ex)
            {
                Log($"Error: While converting json string '{JSONString}', got: " + ex.Msg());
            }
            finally
            {
            }

            return Ret;

        }
    }
}

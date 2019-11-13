namespace SwaggerWcf.Models
{
    public struct TypeFormat
    {
        public ParameterType Type;

        public string Format;

        public string TypeName;

        public TypeFormat(ParameterType type, string format, string typeName = null)
        {
            Type = type;
            Format = format;
            TypeName = typeName;
        }

        internal bool IsPrimitiveType => Type == ParameterType.Boolean ||
                                         Type == ParameterType.Integer ||
                                         Type == ParameterType.Number ||
                                         Type == ParameterType.String && !string.Equals(Format, "stream");

        // possible that enum should be included in primitive type?
        internal bool IsEnum =>  Format == "enum";
    }
}
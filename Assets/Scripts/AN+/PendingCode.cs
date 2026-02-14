using System;

namespace AN_
{
    [Serializable]
    public struct PendingCode
    {
        public CodeType type;
        public string sourceRequestId;
        public string codeValue;

        public bool IsValid => !string.IsNullOrWhiteSpace(codeValue);
    }
}
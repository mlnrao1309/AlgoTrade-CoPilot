using System;
using System.Text.Json;
using AlgoTrading.RuleEditor.Models;

namespace AlgoTrading.RuleEditor.Services
{
    public static class EditorCopyService
    {
        public static T Copy<T>(T source)
        {
            string document = JsonSerializer.Serialize<T>(source);
            T? copy = JsonSerializer.Deserialize<T>(document);
            if (copy == null)
            {
                throw new InvalidOperationException("The editor could not copy this item.");
            }
            return copy;
        }
    }
}

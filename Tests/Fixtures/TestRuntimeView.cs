using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Void2610.Noema.Tests
{
    public sealed class TestRuntimeView : MonoBehaviour
    {
        [UiNodeSource] private readonly Dictionary<string, Button> _options = new();

        public void Add(string key, Button button) => _options[key] = button;
    }
}

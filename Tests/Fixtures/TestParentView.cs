using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Void2610.Noema.Tests
{
    public sealed class TestParentView : MonoBehaviour
    {
        public Button closeButton;
        public List<TestChildView> children = new();
    }
}

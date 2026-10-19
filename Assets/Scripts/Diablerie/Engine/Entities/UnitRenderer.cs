using Diablerie.Engine.IO.D2Formats;
using UnityEngine;

namespace Diablerie.Engine.Entities
{
    [RequireComponent(typeof(COFRenderer))]
    public class UnitRenderer : MonoBehaviour
    {
        private Unit _unit;
        private COFRenderer _renderer;
        private string _lastMode;
        private string _lastWeaponClass;
        private bool _loaded;
        
        private void Awake()
        {
            _unit = GetComponent<Unit>();
            _renderer = GetComponent<COFRenderer>();
        }

        private void Update()
        {
            var mode = _unit.Mode;
            string weaponClass = _unit.weaponClass;
            if (mode == "DT" || mode == "DD")
            {
                weaponClass = "HTH";
            }

            if (!_loaded || mode != _lastMode || weaponClass != _lastWeaponClass)
            {
                _loaded = true;
                _lastMode = mode;
                _lastWeaponClass = weaponClass;
                _renderer.cof = COF.Load(_unit.basePath, _unit.token, weaponClass, mode);
            }
            _renderer.direction = _unit.DirectionIndex;
            _renderer.frame = _unit.AnimationFrame;
        }
    }
}
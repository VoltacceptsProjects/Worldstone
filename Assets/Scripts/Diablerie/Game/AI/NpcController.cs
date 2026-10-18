using System.Collections;
using Diablerie.Engine;
using Diablerie.Engine.Entities;
using Diablerie.Game.UI;
using UnityEngine;

public class NpcController : MonoBehaviour
{
    private Unit _unit;
    private Vector2 _initialPosition;
    private Coroutine _currentAction;

    void Awake()
    {
        _unit = GetComponent<Unit>();
        Events.UnitInteractionStarted += OnInteractionStarted;
    }

    void OnDestroy()
    {
        Events.UnitInteractionStarted -= OnInteractionStarted;
    }

    private void OnInteractionStarted(Unit target, Unit initiator)
    {
        if (target != _unit || !isActiveAndEnabled)
            return;

        StopCurrentAction();
        _currentAction = StartCoroutine(Interact(initiator));
    }

    void OnEnable()
    {
        _initialPosition = _unit.iso.pos;
        _currentAction = StartCoroutine(WalkAround());
    }

    void OnDisable()
    {
        StopCurrentAction();
    }

    private void StopCurrentAction()
    {
        if (_currentAction != null)
            StopCoroutine(_currentAction);
        _currentAction = null;
    }

    IEnumerator WalkAround()
    {
        yield return new WaitForEndOfFrame();
        while (true)
        {
            var target = _initialPosition + new Vector2(Random.Range(-8f, 8f), Random.Range(-8f, 8f));
            _unit.GoTo(target);
            yield return new WaitForSeconds(Random.Range(1f, 3f));
        }
    }

    IEnumerator Interact(Unit initiator)
    {
        _unit.StopMoving();
        _unit.LookAt(initiator.iso.pos);
        yield return null;
        yield return new WaitForSeconds(0.5f);
        while (NpcDialog.IsOpenFor(_unit) || GamblePanel.IsVisible)
        {
            _unit.StopMoving();
            _unit.LookAt(initiator.iso.pos);
            yield return null;
        }
        yield return new WaitForSeconds(1f);
        _currentAction = StartCoroutine(WalkAround());
    }
}

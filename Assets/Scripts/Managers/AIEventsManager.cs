using System;
using UnityEngine;

/// <summary>
/// Manager responsible for handling AI-related events and notifications. 
/// This allows for decoupled communication between AI components and other systems that need to react to AI state changes or actions.
/// </summary>
public sealed class AIEventsManager
{
    #region Event Fields

    private event Action idleRoaming;
    private event Action<int> capturePointAvailable;
    private event Action capturePointThreatened;
    private event Action allPointsCaptured;
    private event Action capturingPoint;
    private event Action<int> arrivedAtCapturePoint;
    private event Action defendingPoint;
    private event Action<bool> aiDeathRespawnTriggered;
    private event Action<bool, Transform, Rigidbody> aiAttemptingFire;
    private event Action<float> aiHealthValueChanged;
    private event Action<Transform, Rigidbody> targetDetected;
    private event Action targetLost;
    private event Action requestToStopTicking;

    #endregion

    #region Trigger Methods

    /// <summary>
    /// Notifies listeners that the AI should roam idly.
    /// </summary>
    public void TriggerOnIdleRoaming()
    {
        Action handler = idleRoaming;
        if (handler != null)
        {
            handler.Invoke();
        }
    }

    /// <summary>
    /// Notifies listeners that a capture point is available for capturing.
    /// </summary>
    /// <param name="captureZoneID">The capture zone id.</param>
    public void TriggerOnCapturePointAvailableForCapturing(int captureZoneID)
    {
        Action<int> handler = capturePointAvailable;
        if (handler != null)
        {
            handler.Invoke(captureZoneID);
        }
    }

    /// <summary>
    /// Notifies listeners that the current capture point is threatened by an enemy.
    /// </summary>
    public void TriggerOnCapturePointThreatenedByEnemy()
    {
        Action handler = capturePointThreatened;
        if (handler != null)
        {
            handler.Invoke();
        }
    }

    /// <summary>
    /// Notifies listeners that all points are captured.
    /// </summary>
    public void TriggerOnAllPointsCaptured()
    {
        Action handler = allPointsCaptured;
        if (handler != null)
        {
            handler.Invoke();
        }
    }

    /// <summary>
    /// Notifies listeners that the AI is currently capturing a point.
    /// </summary>
    public void TriggerOnCapturingPoint()
    {
        Action handler = capturingPoint;
        if (handler != null)
        {
            handler.Invoke();
        }
    }

    /// <summary>
    /// Notifies listeners that the AI arrived at a capture point.
    /// </summary>
    /// <param name="captureZoneID">The capture zone id.</param>
    public void TriggerOnArrivedAtCapturePoint(int captureZoneID)
    {
        Action<int> handler = arrivedAtCapturePoint;
        if (handler != null)
        {
            handler.Invoke(captureZoneID);
        }
    }

    /// <summary>
    /// Notifies listeners that the AI is defending a point.
    /// </summary>
    public void TriggerOnDefendingPoint()
    {
        Action handler = defendingPoint;
        if (handler != null)
        {
            handler.Invoke();
        }
    }

    /// <summary>
    /// Notifies listeners that the AI death/respawn flow was triggered.
    /// </summary>
    /// <param name="value">True if triggered, otherwise false.</param>
    public void TriggerOnAIDeathRespawnTriggered(bool value)
    {
        Action<bool> handler = aiDeathRespawnTriggered;
        if (handler != null)
        {
            handler.Invoke(value);
        }
    }

    /// <summary>
    /// Notifies listeners that the AI health value changed.
    /// </summary>
    /// <param name="value">New health value.</param>
    public void TriggerOnAIHealthValueChanged(float value)
    {
        Action<float> handler = aiHealthValueChanged;
        if (handler != null)
        {
            handler.Invoke(value);
        }
    }

    /// <summary>
    /// Notifies listeners that a combat target was detected.
    /// </summary>
    /// <param name="targetPos">Target transform.</param>
    /// <param name="rb">Target rigidbody.</param>
    public void TriggerOnTargetDetected(Transform targetTransform, Rigidbody targetBody)
    {
        Action<Transform, Rigidbody> handler = targetDetected;
        if (handler != null)
        {
            handler.Invoke(targetTransform, targetBody);
        }
    }

    /// <summary>
    /// Notifies listeners that the AI is attempting to fire.
    /// </summary>
    /// <param name="value">True to start firing, false to stop.</param>
    /// <param name="targetPos">Target transform.</param>
    /// <param name="rb">Target rigidbody.</param>
    public void TriggerOnAIAttemptingFire(bool value, Transform targetPos, Rigidbody rb)
    {
        Action<bool, Transform, Rigidbody> handler = aiAttemptingFire;
        if (handler != null)
        {
            handler.Invoke(value, targetPos, rb);
        }
    }

    /// <summary>
    /// Notifies listeners that the combat target was lost.
    /// </summary>
    public void TriggerOnTargetLost()
    {
        Action handler = targetLost;
        if (handler != null)
        {
            handler.Invoke();
        }
    }

    /// <summary>
    /// Notifies listeners that ticking should stop (used to throttle evaluation).
    /// </summary>
    public void TriggerOnRequestToStopTicking()
    {
        Action handler = requestToStopTicking;
        if (handler != null)
        {
            handler.Invoke();
        }
    }

    #endregion

    #region Subscribe Methods

    /// <summary>
    /// Subscribes a listener to idle roaming notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void SubscribeOnIdleRoaming(Action action)
    {
        idleRoaming += action;
    }

    /// <summary>
    /// Subscribes a listener to capture point available notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void SubscribeOnCapturePointAvailableForCapturing(Action<int> action)
    {
        capturePointAvailable += action;
    }

    /// <summary>
    /// Subscribes a listener to capture point threatened notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void SubscribeOnCapturePointThreatenedByEnemy(Action action)
    {
        capturePointThreatened += action;
    }

    /// <summary>
    /// Subscribes a listener to all points captured notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void SubscribeOnAllPointsCaptured(Action action)
    {
        allPointsCaptured += action;
    }

    /// <summary>
    /// Subscribes a listener to capturing point notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void SubscribeOnCapturingPoint(Action action)
    {
        capturingPoint += action;
    }

    /// <summary>
    /// Subscribes a listener to arrived at capture point notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void SubscribeOnArrivedAtCapturePoint(Action<int> action)
    {
        arrivedAtCapturePoint += action;
    }

    /// <summary>
    /// Subscribes a listener to defending point notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void SubscribeOnDefendingPoint(Action action)
    {
        defendingPoint += action;
    }

    /// <summary>
    /// Subscribes a listener to death/respawn notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void SubscribeOnAIDeathRespawnTriggered(Action<bool> action)
    {
        aiDeathRespawnTriggered += action;
    }

    /// <summary>
    /// Subscribes a listener to AI health changed notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void SubscribeOnAIHealthValueChanged(Action<float> action)
    {
        aiHealthValueChanged += action;
    }

    /// <summary>
    /// Subscribes a listener to target detected notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void SubscribeOnTargetDetected(Action<Transform, Rigidbody> action)
    {
        targetDetected += action;
    }

    /// <summary>
    /// Subscribes a listener to attempting fire notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void SubscribeOnAIAttemptingFire(Action<bool, Transform, Rigidbody> action)
    {
        aiAttemptingFire += action;
    }

    /// <summary>
    /// Subscribes a listener to target lost notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void SubscribeOnTargetLost(Action action)
    {
        targetLost += action;
    }

    /// <summary>
    /// Subscribes a listener to stop ticking requests.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void SubscribeOnRequestToStopTicking(Action action)
    {
        requestToStopTicking += action;
    }

    #endregion

    #region Unsubscribe Methods

    /// <summary>
    /// Unsubscribes a listener from idle roaming notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void UnSubscribeOnIdleRoaming(Action action)
    {
        idleRoaming -= action;
    }

    /// <summary>
    /// Unsubscribes a listener from capture point available notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void UnSubscribeOnCapturePointAvailableForCapturing(Action<int> action)
    {
        capturePointAvailable -= action;
    }

    /// <summary>
    /// Unsubscribes a listener from capture point threatened notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void UnSubscribeOnCapturePointThreatenedByEnemy(Action action)
    {
        capturePointThreatened -= action;
    }

    /// <summary>
    /// Unsubscribes a listener from all points captured notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void UnSubscribeOnAllPointsCaptured(Action action)
    {
        allPointsCaptured -= action;
    }

    /// <summary>
    /// Unsubscribes a listener from capturing point notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void UnSubscribeOnCapturingPoint(Action action)
    {
        capturingPoint -= action;
    }

    /// <summary>
    /// Unsubscribes a listener from arrived at capture point notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void UnSubscribeOnArrivedAtCapturePoint(Action<int> action)
    {
        arrivedAtCapturePoint -= action;
    }

    /// <summary>
    /// Unsubscribes a listener from defending point notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void UnSubscribeOnDefendingPoint(Action action)
    {
        defendingPoint -= action;
    }

    /// <summary>
    /// Unsubscribes a listener from death/respawn notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void UnSubscribeOnAIDeathRespawnTriggered(Action<bool> action)
    {
        aiDeathRespawnTriggered -= action;
    }

    /// <summary>
    /// Unsubscribes a listener from AI health changed notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void UnSubscribeOnAIHealthValueChanged(Action<float> action)
    {
        aiHealthValueChanged -= action;
    }

    /// <summary>
    /// Unsubscribes a listener from target detected notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void UnSubscribeOnTargetDetected(Action<Transform, Rigidbody> action)
    {
        targetDetected -= action;
    }

    /// <summary>
    /// Unsubscribes a listener from attempting fire notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void UnSubscribeOnAIAttemptingFire(Action<bool, Transform, Rigidbody> action)
    {
        aiAttemptingFire -= action;
    }

    /// <summary>
    /// Unsubscribes a listener from target lost notifications.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void UnSubscribeOnTargetLost(Action action)
    {
        targetLost -= action;
    }

    /// <summary>
    /// Unsubscribes a listener from stop ticking requests.
    /// </summary>
    /// <param name="action">Listener callback.</param>
    public void UnSubscribeOnRequestToStopTicking(Action action)
    {
        requestToStopTicking -= action;
    }

    #endregion
}
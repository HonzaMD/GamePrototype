using Assets.Scripts.Bases;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Core
{
    public class ThrowController : MonoBehaviour
    {
        public InputController InputController;

        public Transform[] HoldMarkers;
        public Transform LongThrowMarker;
        private int holdMarkersToken;
        private readonly Action<object, int> DeactivateHoldMarkerA;

        private bool throwActive;
        private float throwForce = 0.5f;
        private float throwTimer;
        private Vector2 throwVector;

        public bool ThrowActive => throwActive;

        public ThrowController()
        {
            DeactivateHoldMarkerA = DeactivateHoldMarker;
        }


        private Transform GetHoldMarker(float life)
        {
            foreach (var hm in HoldMarkers)
            {
                if (!hm.gameObject.activeInHierarchy)
                {
                    hm.gameObject.SetActive(true);
                    Game.Instance.Timer.Plan(DeactivateHoldMarkerA, life, hm.gameObject, holdMarkersToken);
                    return hm;
                }
            }
            return null;
        }

        private void DeactivateHoldMarker(object prm, int token)
        {
            if (token == holdMarkersToken)
                ((GameObject)prm).SetActive(false);
        }

        private void ClearAllHoldMarkers()
        {
            holdMarkersToken++;
            foreach (var hm in HoldMarkers)
            {
                if (hm.gameObject.activeInHierarchy)
                    hm.gameObject.SetActive(false);
            }
        }

        public void SetThrowActive(bool activate, bool throwIt, Character3 character)
        {
            if (throwActive != activate)
            {
                throwActive = activate;
                if (throwActive)
                {
                    throwTimer = 0;
                    LongThrowMarker.gameObject.SetActive(true);
                    PositionLongThrowMarker(character);
                }
                else
                {
                    ClearAllHoldMarkers();
                    LongThrowMarker.gameObject.SetActive(false);
                    if (throwIt)
                    {
                        var body = character.GetHoldObject();
                        if (body != null)
                        {
                            ActivateByThrow(body);
                            character.ThrowObj(body);
                        }
                    }
                }
            }
        }

        private void ActivateByThrow(Label body)
        {
            if (body.KsidGet.IsChildOf(Ksid.ActivatesByThrow) && body.TryGetComponent(out ICanActivate ao))
                ao.Activate();
        }

        public void PositionLongThrowMarker(Character3 character)
        {
            var body = character.GetHoldObject();
            if (body != null)
            {
                UpdateThrowVector(body.transform.position);
                LongThrowMarker.position = body.transform.position;
                LongThrowMarker.rotation = Quaternion.FromToRotation(Vector3.right, throwVector);
                var child = LongThrowMarker.GetChild(0);
                child.localScale = new Vector3(throwForce * 0.4f, child.localScale.y, child.localScale.z);
                child.localPosition = new Vector3(throwForce * 0.2f + 0.4f, child.localPosition.y, child.localPosition.z);
            }
        }

        public void ShowThrowMarker(Character3 character, Vector3 characterVelocity)
        {
            if (throwTimer <= 0)
            {
                var marker = GetHoldMarker(1.8f);
                if (marker != null)
                {
                    throwTimer = 0.3f;
                    var body = character.GetHoldObject();
                    if (body != null)
                    {
                        marker.transform.position = body.transform.position;
                        var mBody = marker.GetComponent<Rigidbody>();
                        var throwMass = body.Rigidbody.mass;
                        mBody.mass = throwMass;
                        Vector2 force = ComputeThrowForce(throwMass);
                        mBody.linearVelocity = (Vector3)force + characterVelocity;
                    }
                }
            }
            else
            {
                throwTimer -= Time.deltaTime;
            }
        }

        public Vector2 ComputeThrowForce(float throwMass)
        {
            var koef = Mathf.Min(5.5f, 10f / Mathf.Sqrt(throwMass));
            return throwVector * throwForce * koef;
        }

        private void UpdateThrowVector(Vector3 startPos)
        {
            var mousePos = InputController.GetMousePosOnZPlane(startPos.z);
            var dir = mousePos - startPos;
            throwForce = dir.magnitude;
            throwVector = dir / throwForce;
            throwForce = Mathf.Clamp(throwForce, 0f, 3f) * 2 / 3;
        }
    }
}

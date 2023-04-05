using System;
using UnityEngine;

#pragma warning disable 649
namespace UnityStandardAssets._2D
{
    public class PlatformerCharacter2D : MonoBehaviour
    {
        [SerializeField] private float m_MaxSpeed = 10f;                    // The fastest the player can travel in the x axis.
        [Range(0, 1)] [SerializeField] private float m_CrouchSpeed = .36f;  // Amount of maxSpeed applied to crouching movement. 1 = 100%
        [SerializeField] private bool m_AirControl = false;                 // Whether or not a player can steer while jumping;
        [SerializeField] private LayerMask m_WhatIsGround;                  // A mask determining what is ground to the character


        [Header("Jumping Settings")]
        [Tooltip("Vertical force when jumping from the ground")]
        [SerializeField] private float m_JumpForce = 800f;                  // Amount of force added when the player jumps.
        [Tooltip("Vertical force when jumping in air")]
        [SerializeField] private uint airJumpForce = 700;
        [Tooltip("Maximum amount of air jumps")]
        [SerializeField] private uint maxAirJumpCount = 1;
        [Tooltip("Maximum amount of force the player can load. This value is used only when the player in crouching and maintaining the jump key")]
        [SerializeField] private float m_MaxLoadJumpForce = 1800f;

        private Transform m_GroundCheck;    // A position marking where to check if the player is grounded.
        const float k_GroundedRadius = .2f; // Radius of the overlap circle to determine if grounded
        public bool m_Grounded;            // Whether or not the player is grounded. Public because used by camera

        private Transform m_CeilingCheck;   // A position marking where to check for ceilings
        const float k_CeilingRadius = .01f; // Radius of the overlap circle to determine if the player can stand up

        private Transform m_WallCheck;      // A position marking where to check if the player is touching a wall
        private bool m_WallTouch;           // Wheter or not the player is touching a wall
        private bool m_WallJumping = false;
        private float m_xWallJumpForce = 400f;                  // Amount of force added when the player jumps.
        private float m_yWallJumpForce = 400f;                  // Amount of force added when the player jumps in the air.

        private Animator m_Anim;            // Reference to the player's animator component.
        private Rigidbody2D m_Rigidbody2D;
        private bool m_FacingRight = true;  // For determining which way the player is currently facing.
        private int m_TouchingLeftOrRight;

        private uint _currAirJumpCount = 0;
        private DateTime? _jumpLoadingTime;
        private bool m_hasLoadedForce = false;
        private float maxTimeLoadJumpForce = 2f; // number of seconds above which the player will get the max load jump force 

        private void Awake()
        {
            // Setting up references.
            m_GroundCheck = transform.Find("GroundCheck");
            m_CeilingCheck = transform.Find("CeilingCheck");
            m_WallCheck = transform.Find("WallCheck");

            m_Anim = GetComponent<Animator>();
            m_Rigidbody2D = GetComponent<Rigidbody2D>();
        }

        private void FixedUpdate()
        {
            m_Grounded = false;
            m_WallTouch = false;

            // The player is grounded if a circlecast to the groundcheck position hits anything designated as ground
            // This can be done using layers instead but Sample Assets will not overwrite your project settings.
            Collider2D[] groundColliders = Physics2D.OverlapCircleAll(m_GroundCheck.position, k_GroundedRadius, m_WhatIsGround);
            foreach (var collider in groundColliders)
            {
                if (collider.gameObject != gameObject)
                {
                    m_Grounded = true;
                    _currAirJumpCount = 0;
                }
            }

            Collider2D[] wallColliders = Physics2D.OverlapCircleAll(m_WallCheck.position, k_GroundedRadius, m_WhatIsGround);
            foreach (var collider in wallColliders)
            {
                if (collider.gameObject != gameObject)
                {
                    m_WallTouch = true;
                }
            }

            if (m_WallJumping)
            {
                Invoke("SetWallJumpingFalse", 0.3f);
            }

            m_Anim.SetBool("Ground", m_Grounded);

            // Set the vertical animation
            m_Anim.SetFloat("vSpeed", m_Rigidbody2D.velocity.y);
        }


        public void Move(float move, bool crouch, bool jump, bool loadJump)
        {
            // If crouching, check to see if the character can stand up
            if (!crouch && m_Anim.GetBool("Crouch"))
            {
                // If the character has a ceiling preventing them from standing up, keep them crouching
                if (Physics2D.OverlapCircle(m_CeilingCheck.position, k_CeilingRadius, m_WhatIsGround))
                {
                    crouch = true;
                }
            }

            // Set whether or not the character is crouching in the animator
            m_Anim.SetBool("Crouch", crouch);

            //only control the player if grounded or airControl is turned on
            if ((m_Grounded || m_AirControl) && !m_WallJumping)
            {
                // Reduce the speed if crouching by the crouchSpeed multiplier
                move = (crouch ? move * m_CrouchSpeed : move);

                // The Speed animator parameter is set to the absolute value of the horizontal input.
                m_Anim.SetFloat("Speed", Mathf.Abs(move));

                // Move the character


                m_Rigidbody2D.velocity = new Vector2(move * m_MaxSpeed, m_Rigidbody2D.velocity.y);

                // If the input is moving the player right and the player is facing left...
                if (move > 0 && !m_FacingRight)
                {
                    // ... flip the player.
                    Flip();
                }
                // Otherwise if the input is moving the player left and the player is facing right...
                else if (move < 0 && m_FacingRight)
                {
                    // ... flip the player.
                    Flip();
                }
            }
            if (crouch && loadJump)
            {
                Debug.Log("Load jump");
                LoadJump();
            }
            else if (jump && !crouch && m_Grounded)
            {
                Debug.Log("Jump");
                Jump();
            }
            else if (jump && !crouch && !m_Grounded && !m_WallTouch)
            {
                Debug.Log("Air jump");
                AirJump();
            }
            else if (m_hasLoadedForce)
            {
                Debug.Log("Apply Air jump");
                ApplyLoadedJump();
            }
            else if (!m_Grounded && jump && m_WallTouch)
            {
                Debug.Log(move);
                Debug.Log("Wall jump");
                WallJump();
            }
        }

        private void SetWallJumpingFalse()
        {
            m_WallJumping = false;
        }

        public void LoadJump()
        {
            // Saving the time at which the loading started
            _jumpLoadingTime ??= DateTime.Now;
            m_hasLoadedForce = true;
        }

        public void ApplyLoadedJump()
        {
            if (_jumpLoadingTime == null) return;
            // Resetting the animation
            m_Anim.SetBool("Ground", false);
            m_Anim.SetBool("Crouch", false);

            // calculating the ratio of the max loaded force
            var time = DateTime.Now;
            var timeDiff = time - _jumpLoadingTime;
            var ratio = 1 + timeDiff.Value.Seconds / maxTimeLoadJumpForce;

            // applying the force
            var horizontalForce = Math.Min(m_JumpForce * ratio, m_MaxLoadJumpForce);
            m_Rigidbody2D.AddForce(new Vector2(0f, horizontalForce));
            _jumpLoadingTime = null;
            m_hasLoadedForce = false;
        }

        public void Jump()
        {
            if (m_Grounded && m_Anim.GetBool("Ground") && !m_hasLoadedForce)
            {
                // Add a vertical force to the player.
                m_Grounded = false;
                m_Anim.SetBool("Ground", false);
                m_Rigidbody2D.AddForce(new Vector2(0f, m_JumpForce));

            }
        }

        public void AirJump()
        {
            if (_currAirJumpCount < maxAirJumpCount && !m_Grounded && !m_hasLoadedForce)
            {
                m_Rigidbody2D.AddForce(new Vector2(0f, airJumpForce));
                _currAirJumpCount += 1;
            }
        }

        public void WallJump()
        {
            if (!m_FacingRight)
            {
                m_TouchingLeftOrRight = 1;
            }
            else if (m_FacingRight)
            {
                m_TouchingLeftOrRight = -1;
            }
            m_WallJumping = true;
            m_Grounded = false;
            m_Anim.SetBool("Ground", false);
            m_Rigidbody2D.velocity = new Vector2(m_TouchingLeftOrRight, 1);
            m_Rigidbody2D.AddForce(new Vector2(m_xWallJumpForce * m_TouchingLeftOrRight, m_yWallJumpForce));
            Flip();
        }



        private void Flip()
        {
            // Switch the way the player is labelled as facing.
            m_FacingRight = !m_FacingRight;

            // Multiply the player's x local scale by -1.
            Vector3 theScale = transform.localScale;
            theScale.x *= -1;
            transform.localScale = theScale;
        }
    }
}
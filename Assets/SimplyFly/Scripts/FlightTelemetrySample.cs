using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SimplyFly.GroundOps
{
    [Serializable]
    public sealed class FlightTelemetrySample
    {
        public string timestamp;
        public float elapsedTime;
        public string scenario;
        public Vector3 position;
        public Vector3 velocity;
        public string flightState;
        public string engineState;
        public bool grounded;
        public float throttlePercent;
        public float engineRPM;
        public float thrustForce;
        public float airspeedKnots;
        public float groundSpeedKnots;
        public float forwardVelocity;
        public float lateralVelocity;
        public float pitchAngle;
        public float bankAngle;
        public float heading;
        public float pitchRate;
        public float rollRate;
        public float yawRate;
        public float altitude;
        public float altitudeAboveRunway;
        public float verticalSpeed;
        public float flightPathAngle;
        public float angleOfAttack;
        public float liftForce;
        public float liftPercentWeight;
        public float verticalLiftPercent;
        public float sideLiftForce;
        public float dragForce;
        public float rollingResistance;
        public float pitchInput;
        public float rollInput;
        public float steeringInput;
        public float rudderInput;
        public float slipAmount;
        public float slipBallPosition;
        public float brakeInput;
        public bool trimActive;
        public float trimValue;
        public float flapAngle;
        public string stallState;
        public bool stallWarning;
        public float currentStallSpeed;
        public float controlAuthorityMultiplier;
        public float stallLiftMultiplier;
        public string airport;
        public float distanceToThreshold;
        public float runwayAlignmentError;
        public float centerlineOffset;
        public float glideSlopeAngle;
        public string landingState;
        public bool mainWheelsTouching;
        public bool noseWheelTouching;
        public bool allWheelsTouching;
        public float touchdownVerticalSpeed;
        public float touchdownAirspeed;
        public float touchdownPitch;
        public float touchdownBank;
        public string landingQuality;
        public bool mainWheelsFirst;
        public bool noseFirstWarning;
        public float bounceVelocity;

        public static string CsvHeader =>
            "timestamp,elapsed_time,scenario,pos_x,pos_y,pos_z," +
            "vel_x,vel_y,vel_z,flight_state,engine_state,grounded," +
            "throttle_percent,engine_rpm,thrust_force,airspeed_kt," +
            "ground_speed_kt,forward_velocity,lateral_velocity,pitch_deg," +
            "bank_deg,heading_deg,pitch_rate_deg_s,roll_rate_deg_s," +
            "yaw_rate_deg_s,altitude,altitude_above_runway,vertical_speed," +
            "flight_path_deg,aoa_deg,lift_force,lift_percent_weight," +
            "vertical_lift_percent,side_lift_force,drag_force," +
            "rolling_resistance,pitch_input,roll_input,steering_input," +
            "rudder_input,slip_amount,slip_ball_position,brake_input," +
            "trim_active,trim_value,flap_angle,stall_state," +
            "stall_warning,current_stall_speed,control_authority_multiplier," +
            "stall_lift_multiplier,airport,distance_to_threshold," +
            "alignment_error,centerline_offset,glide_slope_deg,landing_state," +
            "main_wheels_touching,nose_wheel_touching,all_wheels_touching," +
            "touchdown_vertical_speed,touchdown_airspeed_kt,touchdown_pitch," +
            "touchdown_bank,landing_quality,main_wheels_first," +
            "nose_first_warning,bounce_velocity";

        public string ToCsvRow()
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder row = new StringBuilder(768);
            void Add(object value)
            {
                if (row.Length > 0) row.Append(',');
                string text = value switch
                {
                    float f => f.ToString("0.####", c),
                    double d => d.ToString("0.####", c),
                    bool b => b ? "true" : "false",
                    null => string.Empty,
                    _ => value.ToString()
                };
                if (text.Contains(",") || text.Contains("\"") ||
                    text.Contains("\n"))
                {
                    row.Append('"');
                    row.Append(text.Replace("\"", "\"\""));
                    row.Append('"');
                }
                else row.Append(text);
            }

            Add(timestamp); Add(elapsedTime); Add(scenario);
            Add(position.x); Add(position.y); Add(position.z);
            Add(velocity.x); Add(velocity.y); Add(velocity.z);
            Add(flightState); Add(engineState); Add(grounded);
            Add(throttlePercent); Add(engineRPM); Add(thrustForce);
            Add(airspeedKnots); Add(groundSpeedKnots);
            Add(forwardVelocity); Add(lateralVelocity);
            Add(pitchAngle); Add(bankAngle); Add(heading);
            Add(pitchRate); Add(rollRate); Add(yawRate);
            Add(altitude); Add(altitudeAboveRunway); Add(verticalSpeed);
            Add(flightPathAngle); Add(angleOfAttack);
            Add(liftForce); Add(liftPercentWeight);
            Add(verticalLiftPercent); Add(sideLiftForce); Add(dragForce);
            Add(rollingResistance); Add(pitchInput); Add(rollInput);
            Add(steeringInput); Add(rudderInput); Add(slipAmount);
            Add(slipBallPosition); Add(brakeInput); Add(trimActive);
            Add(trimValue); Add(flapAngle); Add(stallState);
            Add(stallWarning); Add(currentStallSpeed);
            Add(controlAuthorityMultiplier); Add(stallLiftMultiplier);
            Add(airport); Add(distanceToThreshold);
            Add(runwayAlignmentError); Add(centerlineOffset);
            Add(glideSlopeAngle); Add(landingState);
            Add(mainWheelsTouching); Add(noseWheelTouching);
            Add(allWheelsTouching); Add(touchdownVerticalSpeed);
            Add(touchdownAirspeed); Add(touchdownPitch);
            Add(touchdownBank); Add(landingQuality);
            Add(mainWheelsFirst); Add(noseFirstWarning); Add(bounceVelocity);
            return row.ToString();
        }
    }
}

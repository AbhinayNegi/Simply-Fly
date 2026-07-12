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
        public bool trimEnabled;
        public bool pitchHoldEnabled;
        public bool pitchHoldActive;
        public float pitchHoldTargetPitch;
        public float pitchHoldElevatorCommand;
        public bool vsHoldEnabled;
        public bool vsHoldActive;
        public float vsHoldTargetVerticalSpeed;
        public float vsHoldElevatorCommand;
        public string activeAssistMode;
        public string assistDisengageReason;
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
        public bool navigationActive;
        public string navigationPhase;
        public string departureAirport;
        public string arrivalAirport;
        public string nextWaypoint;
        public float navigationDistance;
        public float desiredHeading;
        public float navigationHeadingError;
        public float routeProgressPercent;
        public float navigationRunwayHeading;
        public float navigationThresholdDistance;
        public float navigationLateralOffset;
        public float navigationAlignmentError;
        public float targetGlideSlopeAngle;
        public float targetGlideSlopeAltitude;
        public float glideSlopeAltitudeError;
        public bool approachHelperActive;
        public string sideViewHelperStatus;
        public string topDownHelperStatus;
        public bool approachValidationEnabled;
        public string selectedArrivalRunway;
        public bool approachClear;
        public string approachBlockedReason;
        public string blockingObjectName;
        public float blockingObjectDistanceFromThreshold;
        public float terrainClearanceMin;
        public float approachCorridorWidth;
        public float finalApproachFixDistance;
        public float selectedGlideSlopeDegrees;
        public float touchdownTargetDistanceFromThreshold;
        public float distanceToTouchdownTarget;
        public float thresholdCrossingHeight;
        public float sideViewZoomLevel;
        public string sideViewMode;
        public float currentTargetAltitude;
        public string altitudeReferenceType;
        public float currentAltitudeMSL;
        public float currentAltitudeAGL;
        public float navigationAltitudeError;
        public string verticalNavigationPhase;
        public float suggestedVerticalSpeed;
        public float routeProfileDistance;
        public float routeProfileTargetAltitude;
        public float terrainClearanceBelowAircraft;
        public float minimumTerrainClearanceCurrentSegment;
        public string approachGuidanceMode;
        public bool trajectoryVisualizerActive;
        public bool predictedImpactValid;
        public Vector3 predictedImpactWorldPosition;
        public float predictedImpactDistanceToThreshold;
        public float predictedImpactDistanceFromCenterline;
        public string predictedImpactStatus;
        public float predictionTimeToImpact;
        public float trajectoryPredictionSeconds;
        public float currentFlightPathAngle;
        public float currentGroundTrack;
        public float currentVerticalSpeed;
        public float windFromHeading;
        public float windSpeedKnots;
        public float headwindKnots;
        public float crosswindKnots;

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
            "trim_active,trim_value,trim_enabled,pitch_hold_enabled," +
            "pitch_hold_active,pitch_hold_target_pitch," +
            "pitch_hold_elevator_command,vs_hold_enabled,vs_hold_active," +
            "vs_hold_target_vertical_speed,vs_hold_elevator_command," +
            "active_assist_mode,assist_disengage_reason,flap_angle,stall_state," +
            "stall_warning,current_stall_speed,control_authority_multiplier," +
            "stall_lift_multiplier,airport,distance_to_threshold," +
            "alignment_error,centerline_offset,glide_slope_deg,landing_state," +
            "main_wheels_touching,nose_wheel_touching,all_wheels_touching," +
            "touchdown_vertical_speed,touchdown_airspeed_kt,touchdown_pitch," +
            "touchdown_bank,landing_quality,main_wheels_first," +
            "nose_first_warning,bounce_velocity,navigation_active," +
            "navigation_phase,departure_airport,arrival_airport,next_waypoint," +
            "navigation_distance,desired_heading,navigation_heading_error," +
            "route_progress_percent,navigation_runway_heading," +
            "navigation_threshold_distance,navigation_lateral_offset," +
            "navigation_alignment_error,target_glide_slope_angle," +
            "target_glide_slope_altitude,glide_slope_altitude_error," +
            "approach_helper_active,side_view_helper_status," +
            "top_down_helper_status,approach_validation_enabled," +
            "selected_arrival_runway,approach_clear,approach_blocked_reason," +
            "blocking_object_name,blocking_object_distance_from_threshold," +
            "terrain_clearance_min,approach_corridor_width," +
            "final_approach_fix_distance,selected_glide_slope_degrees," +
            "touchdown_target_distance_from_threshold," +
            "distance_to_touchdown_target,threshold_crossing_height," +
            "side_view_zoom_level,side_view_mode,current_target_altitude," +
            "altitude_reference_type,current_altitude_msl,current_altitude_agl," +
            "navigation_altitude_error,vertical_navigation_phase," +
            "suggested_vertical_speed,route_profile_distance," +
            "route_profile_target_altitude,terrain_clearance_below_aircraft," +
            "minimum_terrain_clearance_current_segment," +
            "approach_guidance_mode,trajectory_visualizer_active," +
            "predicted_impact_valid,predicted_impact_x,predicted_impact_y," +
            "predicted_impact_z,predicted_impact_distance_to_threshold," +
            "predicted_impact_distance_from_centerline," +
            "predicted_impact_status,prediction_time_to_impact," +
            "trajectory_prediction_seconds,current_flight_path_angle," +
            "current_ground_track,current_vertical_speed," +
            "wind_from_heading,wind_speed_kt,headwind_kt,crosswind_kt";

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
            Add(trimValue); Add(trimEnabled); Add(pitchHoldEnabled);
            Add(pitchHoldActive); Add(pitchHoldTargetPitch);
            Add(pitchHoldElevatorCommand); Add(vsHoldEnabled);
            Add(vsHoldActive); Add(vsHoldTargetVerticalSpeed);
            Add(vsHoldElevatorCommand); Add(activeAssistMode);
            Add(assistDisengageReason); Add(flapAngle); Add(stallState);
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
            Add(navigationActive); Add(navigationPhase);
            Add(departureAirport); Add(arrivalAirport); Add(nextWaypoint);
            Add(navigationDistance); Add(desiredHeading);
            Add(navigationHeadingError); Add(routeProgressPercent);
            Add(navigationRunwayHeading); Add(navigationThresholdDistance);
            Add(navigationLateralOffset); Add(navigationAlignmentError);
            Add(targetGlideSlopeAngle); Add(targetGlideSlopeAltitude);
            Add(glideSlopeAltitudeError); Add(approachHelperActive);
            Add(sideViewHelperStatus); Add(topDownHelperStatus);
            Add(approachValidationEnabled); Add(selectedArrivalRunway);
            Add(approachClear); Add(approachBlockedReason);
            Add(blockingObjectName); Add(blockingObjectDistanceFromThreshold);
            Add(terrainClearanceMin); Add(approachCorridorWidth);
            Add(finalApproachFixDistance); Add(selectedGlideSlopeDegrees);
            Add(touchdownTargetDistanceFromThreshold);
            Add(distanceToTouchdownTarget); Add(thresholdCrossingHeight);
            Add(sideViewZoomLevel); Add(sideViewMode);
            Add(currentTargetAltitude); Add(altitudeReferenceType);
            Add(currentAltitudeMSL); Add(currentAltitudeAGL);
            Add(navigationAltitudeError); Add(verticalNavigationPhase);
            Add(suggestedVerticalSpeed); Add(routeProfileDistance);
            Add(routeProfileTargetAltitude);
            Add(terrainClearanceBelowAircraft);
            Add(minimumTerrainClearanceCurrentSegment);
            Add(approachGuidanceMode); Add(trajectoryVisualizerActive);
            Add(predictedImpactValid);
            Add(predictedImpactWorldPosition.x);
            Add(predictedImpactWorldPosition.y);
            Add(predictedImpactWorldPosition.z);
            Add(predictedImpactDistanceToThreshold);
            Add(predictedImpactDistanceFromCenterline);
            Add(predictedImpactStatus);
            Add(predictionTimeToImpact);
            Add(trajectoryPredictionSeconds);
            Add(currentFlightPathAngle);
            Add(currentGroundTrack);
            Add(currentVerticalSpeed);
            Add(windFromHeading); Add(windSpeedKnots);
            Add(headwindKnots); Add(crosswindKnots);
            return row.ToString();
        }
    }
}

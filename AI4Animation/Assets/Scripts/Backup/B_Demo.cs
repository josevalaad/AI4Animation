﻿using UnityEngine;

public class B_Demo : MonoBehaviour {

	public Transform ChainStart;

	public float Scale = 1f;

	public float Stand, Walk, Jog, Crouch, Jump;

	private float Phase = 0f;

	private B_PFNN Network;
	private B_Character Character;
	private B_Trajectory Trajectory;

	private const float M_PI = 3.14159265358979323846f;

	void Start() {
		Network = GetComponent<B_PFNN>();
		Network.Build();
		Character = GetComponent<B_Character>();
		Trajectory = GetComponent<B_Trajectory>();
		Predict();
	}

	void OnValidate() {
		Scale = Mathf.Max(1e-5f, Scale);
	}

	void Update() {
		int size = 120;
		transform.position = Trajectory.Points[size/2].Position;
		transform.rotation = Trajectory.Points[size/2].Rotation;
		
		//Get user input
		float rotate = 0f; 
		if(Input.GetKey(KeyCode.Q)) {
			rotate -= 1f;
		}
		if(Input.GetKey(KeyCode.E)) {
			rotate += 1f;
		}
		Vector3 direction = Vector3.zero;
		if(Input.GetKey(KeyCode.W)) {
			direction.z += 1f;
		}
		if(Input.GetKey(KeyCode.S)) {
			direction.z -= 1f;
		}
		if(Input.GetKey(KeyCode.A)) {
			direction.x -= 1f;
		}
		if(Input.GetKey(KeyCode.D)) {
			direction.x += 1f;
		}
		direction = direction.normalized;

		//Predict trajectory
		Trajectory.Target(rotate, direction);
		Trajectory.Predict();

		//Query PFNN
		Predict();

		//Correct trajectory
		
		float updateX = Network.GetOutput(0) / Scale;
		float updateZ = Network.GetOutput(1) / Scale;
		float angle = Network.GetOutput(2);
		int capacity = 0;
		for(int i=size/2+1; i<size; i++) {
			capacity += 8;
		}
		float[] future = new float[capacity];
		for(int i=size/2+1; i<size; i++) {
			int w = (Trajectory.Size/2)/10;
			int k = i - (size/2+1);
			future[8*k+0] = Network.GetOutput(8+(w*0)+(i/10)-w) / Scale;
			future[8*k+1] = Network.GetOutput(8+(w*0)+(i/10)-w+1) / Scale;
			future[8*k+2] = Network.GetOutput(8+(w*1)+(i/10)-w) / Scale;
			future[8*k+3] = Network.GetOutput(8+(w*1)+(i/10)-w+1) / Scale;
			future[8*k+4] = Network.GetOutput(8+(w*2)+(i/10)-w);
			future[8*k+5] = Network.GetOutput(8+(w*2)+(i/10)-w+1);
			future[8*k+6] = Network.GetOutput(8+(w*3)+(i/10)-w);
			future[8*k+7] = Network.GetOutput(8+(w*3)+(i/10)-w+1);
		}
		Trajectory.Correct(updateX, updateZ, angle, future);
	}

	private void Predict() {
		////////////////////////////////////////
		////////// Input
		////////////////////////////////////////

		//Input Trajectory Positions / Directions
		for(int i=0; i<Trajectory.Size; i+=10) {
			int w = (Trajectory.Size)/10;
			Vector3 pos = Trajectory.Points[i].GetRelativePosition();
			Vector3 dir = Trajectory.Points[i].GetRelativeDirection();
			Network.SetInput((w*0)+i/10, Scale * pos.x);
			Network.SetInput((w*1)+i/10, Scale * pos.z);
			Network.SetInput((w*2)+i/10, dir.x);
			Network.SetInput((w*3)+i/10, dir.z);
		}

		//Input Trajectory Gaits
		for (int i=0; i<Trajectory.Size; i+=10) {
			int w = (Trajectory.Size)/10;
			Network.SetInput((w*4)+i/10, Stand);
			Network.SetInput((w*5)+i/10, Walk);
			Network.SetInput((w*6)+i/10, Jog);
			Network.SetInput((w*7)+i/10, Crouch);
			Network.SetInput((w*8)+i/10, Jump);
			Network.SetInput((w*9)+i/10, 0f);
		}

		//Input Joint Previous Positions / Velocities / Rotations
		for(int i=0; i<Character.Joints.Length; i++) {
			int o = (((Trajectory.Size)/10)*10);  

			Vector3 pos = Character.Joints[i].GetRelativePosition();
			Vector3 vel = Character.Joints[i].GetRelativeVelocity();
			
			Network.SetInput(o+(Character.Joints.Length*3*0)+i*3+0, Scale * pos.x);
			Network.SetInput(o+(Character.Joints.Length*3*0)+i*3+1, Scale * pos.y);
			Network.SetInput(o+(Character.Joints.Length*3*0)+i*3+2, Scale * pos.z);
			Network.SetInput(o+(Character.Joints.Length*3*1)+i*3+0, Scale * vel.x);
			Network.SetInput(o+(Character.Joints.Length*3*1)+i*3+1, Scale * vel.y);
			Network.SetInput(o+(Character.Joints.Length*3*1)+i*3+2, Scale * vel.z);
		}

		//Input Trajectory Heights
		for (int i=0; i<Trajectory.Size; i+=10) {
			int o = (((Trajectory.Size)/10)*10) + Character.Joints.Length*3*2;
			int w = Trajectory.Size/10;
			Network.SetInput(o+(w*0)+(i/10), Scale * (Trajectory.Points[i].ProjectLeft(Trajectory.Width/2f).y - Character.transform.position.y));
			Network.SetInput(o+(w*1)+(i/10), Scale * (Trajectory.Points[i].Position.y - Character.transform.position.y));
			Network.SetInput(o+(w*2)+(i/10), Scale * (Trajectory.Points[i].ProjectRight(Trajectory.Width/2f).y - Character.transform.position.y));
		}

		////////////////////////////////////////
		////////// Predict
		////////////////////////////////////////
		Phase += 2f * Time.deltaTime;
		Phase = Mathf.Repeat(Phase, 2f*Mathf.PI);
		//float stand_amount = Mathf.Pow(1.0f-Trajectory.Points[Trajectory.Size/2].Height, 0.25f);
		//Phase  = Mathf.Repeat(Phase + (stand_amount * 0.9f + 0.1f) * 2f*Mathf.PI * Network.GetOutput(3), 2f*Mathf.PI);

		Network.Predict(Phase);
		
		////////////////////////////////////////
		////////// Output
		////////////////////////////////////////

		int opos = 8+(((Trajectory.Size/2)/10)*4)+(Character.Joints.Length*3*0);
		int ovel = 8+(((Trajectory.Size/2)/10)*4)+(Character.Joints.Length*3*1);
		for(int i=0; i<Character.Joints.Length; i++) {			
			Vector3 position = new Vector3(Network.GetOutput(opos+i*3+0), Network.GetOutput(opos+i*3+1), Network.GetOutput(opos+i*3+2));
			Vector3 velocity = new Vector3(Network.GetOutput(ovel+i*3+0), Network.GetOutput(ovel+i*3+1), Network.GetOutput(ovel+i*3+2));

			Character.Joints[i].SetRelativePosition((1f / Scale) * position);
			Character.Joints[i].SetRelativeVelocity((1f / Scale) * velocity);
		}
	}

	private Quaternion quat_exp(Vector3 l) {
		float w = l.magnitude;
		Quaternion q = w < 0.01f ? new Quaternion(0f, 0f, 0f, 1f) : new Quaternion(
			l.x * (Mathf.Sin(w) / w),
			l.y * (Mathf.Sin(w) / w),
			l.z * (Mathf.Sin(w) / w),
 			Mathf.Cos(w)
			);
		float div = Mathf.Sqrt(q.w*q.w + q.x*q.x + q.y*q.y + q.z*q.z);
		return new Quaternion(q.x/div, q.y/div, q.z/div, q.w/div);
	}

}
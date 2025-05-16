#[compute]
#version 450

struct PathRequest {
	ivec2 a;
	ivec2 b;
	uint completed;
	uint reserved;
};

layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;

layout(set = 0, binding = 0) readonly buffer InputSize {
    ivec2 resolution;
}
input_size;

layout(set = 0, binding = 1) readonly buffer WaveBuffer {
    uint data[];
} wave_buffer;

layout(set = 0, binding = 2) readonly buffer PathRequestBuffer {
    uint count;
	uint reserve;
	PathRequest data[];
} path_request_buffer;

layout(set = 0, binding = 3) writeonly buffer PathResultBuffer {
	ivec2 data[];
} path_result_buffer;

ivec2 moveDirections[4] = { {-1, 0}, {1, 0}, {0, -1}, {0, 1} };

uint getWaveIndex(uvec2 pos){
	return (gl_GlobalInvocationID.z * input_size.resolution.x * input_size.resolution.y + pos.y * input_size.resolution.x + pos.x);
}

void main() {
    uvec3 id = gl_GlobalInvocationID.xyz;

    ivec2 resolution = input_size.resolution;

    if (id.z >= path_request_buffer.count || path_request_buffer.data[id.z].completed == 0) return;

	PathRequest request = path_request_buffer.data[id.z];

	uint startIndex = 0;
	for(int i = 0; i < id.z; i++)
	{
		startIndex += path_request_buffer.data[i].completed;
	}

	if (startIndex + request.completed - 1 >= path_result_buffer.data.length())
		return;

	//path_result_buffer.data[0] = ivec2(1,1);

	ivec2 point = request.b;

	path_result_buffer.data[startIndex + request.completed - 1] = request.b;

	for (uint i = request.completed - 1; i > 0; i--)
	{
		for(int d = 0; d < moveDirections.length(); d++)
		{
			ivec2 otherVec = point + moveDirections[d];
			if (otherVec.x < 0 || otherVec.y < 0 || otherVec.x >= resolution.x || otherVec.y >= resolution.y)
				continue;
			
			uint otherWaveIndex = getWaveIndex(uvec2(otherVec));
			if (wave_buffer.data[otherWaveIndex] == i)
			{
				point = otherVec;
				path_result_buffer.data[startIndex + i - 1] = otherVec;
				break;
			}
		}
	}
}
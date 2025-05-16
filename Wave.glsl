#[compute]
#version 450

struct PathRequest {
	ivec2 a;
	ivec2 b;
	uint completed;
	uint reserved;
};

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(push_constant) uniform PushConstants {
    int iteration;
 	int reserve0;
 	int reserve1;
	int reserve2;
 } pc;

layout(set = 0, binding = 0) readonly buffer MapBuffer {
    int data[];
}
map_buffer;

layout(set = 0, binding = 1) readonly buffer InputSize {
    ivec2 resolution;
}
input_size;

layout(set = 0, binding = 2) buffer WaveBuffer {
    uint data[];
} wave_buffer;

layout(set = 0, binding = 3) buffer PathRequestBuffer {
    uint count;
	uint reserve;
	PathRequest data[];
} path_request_buffer;

ivec2 moveDirections[4] = { {-1, 0}, {1, 0}, {0, -1}, {0, 1} };

uint getWaveIndex(uvec2 pos){
	return (gl_GlobalInvocationID.z * input_size.resolution.x * input_size.resolution.y + pos.y * input_size.resolution.x + pos.x);
}

uint getMapIndex(uvec2 pos){
	return (pos.x * input_size.resolution.y + pos.y);
}

int getCell(uvec2 pos){
	uint index = getMapIndex(pos);
	int val = map_buffer.data[index/4];
	uint q = (index % 4) * 8;
	return ((val >> q) & 0xFF);
}

void main() {
	if (gl_GlobalInvocationID.z >= path_request_buffer.count)
		return;

	PathRequest req = path_request_buffer.data[gl_GlobalInvocationID.z];

	if (req.completed > 0)
		return;

    uvec3 id = gl_GlobalInvocationID.xyz;

    ivec2 resolution = input_size.resolution;

    if (id.x >= resolution.x || id.y >= resolution.y) return;



	uint mapIndex = getMapIndex(id.xy);

	uint waveIndex = getWaveIndex(id.xy);

	if (pc.iteration == 0)
	{
		if (req.a.x == id.x && req.a.y == id.y)
			wave_buffer.data[waveIndex] = 1;
		else
			wave_buffer.data[waveIndex] = 0;
		return;
	}
	else if (wave_buffer.data[waveIndex] == pc.iteration)
	{
		for(int i = 0; i < moveDirections.length(); i++)
		{
			ivec2 otherVec = ivec2(id.xy) + moveDirections[i];
			if (otherVec.x < 0 || otherVec.y < 0 || otherVec.x >= resolution.x || otherVec.y >= resolution.y)
				continue;
			
			if (getCell(otherVec) > 0)
				continue;
			
			uint otherWaveIndex = getWaveIndex(uvec2(otherVec));
			if (atomicCompSwap(wave_buffer.data[otherWaveIndex], 0, pc.iteration + 1) == 0)
			{

				if (otherVec == req.b)
				{
					atomicCompSwap(path_request_buffer.data[id.z].completed, 0, pc.iteration+1);
				}
			}
		}
	}
}
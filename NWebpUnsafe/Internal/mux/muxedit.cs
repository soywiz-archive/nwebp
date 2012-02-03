using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if false
namespace NWebp.Internal
{
	//------------------------------------------------------------------------------
	// Life of a mux object.
	public partial class WebPMux
	{
		private WebPMux()
		{
			MuxInit();
		}

		// Creates an empty mux object.
		// Returns:
		//   A pointer to the newly created empty mux object.
		static public WebPMux WebPMuxNew() {
		  return new WebPMux();
		}

		public void MuxInit() {
		  Global.memset(mux, 0, sizeof(*mux));
		  this.state_ = WEBP_MUX_STATE_PARTIAL;
		}

		static void DeleteAllChunks(WebPChunk* chunk_list) {
		  while (*chunk_list) {
			*chunk_list = ChunkDelete(*chunk_list);
		  }
		}

		void MuxRelease() {
		  MuxImageDeleteAll(&this.images_);
		  DeleteAllChunks(&this.vp8x_);
		  DeleteAllChunks(&this.iccp_);
		  DeleteAllChunks(&this.loop_);
		  DeleteAllChunks(&this.meta_);
		  DeleteAllChunks(&this.unknown_);
		}

		// Deletes the mux object.
		// Parameters:
		//   mux - (in/out) object to be deleted
		void WebPMuxDelete() {
			this.MuxRelease();
			//free(this);
		}
	}

	class muxedit
	{
		//------------------------------------------------------------------------------
		// Helper method(s).

		// Handy MACRO, makes MuxSet() very symmetric to MuxGet().
		void SWITCH_ID_LIST(ID, LIST) {
		  if (id == (ID)) {                                                        
			err = ChunkAssignDataImageInfo(&chunk, data, size,                     
										   image_info,                             
										   copy_data, kChunks[(ID)].chunkTag);     
			if (err == WEBP_MUX_OK) {                                              
			  err = ChunkSetNth(&chunk, (LIST), nth);                              
			}               
			return err;     
		  }
		}

		static WebPMuxError MuxSet(WebPMux* mux, TAG_ID id, uint nth, byte* data, uint size, WebPImageInfo* image_info, int copy_data)
		{
		  WebPChunk chunk;
		  WebPMuxError err = WEBP_MUX_NOT_FOUND;
		  if (mux == null) return WEBP_MUX_INVALID_ARGUMENT;
		  assert(!IsWPI(id));

		  ChunkInit(&chunk);
		  SWITCH_ID_LIST(VP8X_ID, &mux.vp8x_);
		  SWITCH_ID_LIST(ICCP_ID, &mux.iccp_);
		  SWITCH_ID_LIST(LOOP_ID, &mux.loop_);
		  SWITCH_ID_LIST(META_ID, &mux.meta_);
		  if (id == UNKNOWN_ID && size > TAG_SIZE) {
			// For raw-data unknown chunk, the first four bytes should be the tag to be
			// used for the chunk.
			err = ChunkAssignDataImageInfo(&chunk, data + TAG_SIZE, size - TAG_SIZE,
										   image_info, copy_data, GetLE32(data + 0));
			if (err == WEBP_MUX_OK)
			  err = ChunkSetNth(&chunk, &mux.unknown_, nth);
		  }
		  return err;
		}
		#undef SWITCH_ID_LIST

		static WebPMuxError MuxAddChunk(WebPMux* mux, uint nth, uint tag, byte* data, uint size, WebPImageInfo* image_info, int copy_data)
		{
		  TAG_ID id;
		  assert(mux != null);
		  assert(size <= MAX_CHUNK_PAYLOAD);

		  id = ChunkGetIdFromTag(tag);
		  if (id == NIL_ID) return WEBP_MUX_INVALID_PARAMETER;

		  return MuxSet(mux, id, nth, data, size, image_info, copy_data);
		}

		static void InitImageInfo(WebPImageInfo* image_info) {
		  assert(image_info);
		  memset(image_info, 0, sizeof(*image_info));
		}

		// Creates WebPImageInfo object and sets offsets, dimensions and duration.
		// Dimensions calculated from passed VP8 image data.
		static WebPImageInfo* CreateImageInfo(uint x_offset, uint y_offset, uint duration, byte* data, uint size)
		{
		  int width;
		  int height;
		  WebPImageInfo* image_info = null;

		  if (!VP8GetInfo(data, size, size, &width, &height)) {
			return null;
		  }

		  image_info = (WebPImageInfo*)malloc(sizeof(WebPImageInfo));
		  if (image_info != null) {
			InitImageInfo(image_info);
			image_info.x_offset_ = x_offset;
			image_info.y_offset_ = y_offset;
			image_info.duration_ = duration;
			image_info.width_ = width;
			image_info.height_ = height;
		  }

		  return image_info;
		}

		// Create data for frame/tile given image_info.
		static WebPMuxError CreateDataFromImageInfo(WebPImageInfo* image_info, int is_frame, byte** data, uint* size)
		{
		  assert(data);
		  assert(size);
		  assert(image_info);

		  *size = kChunks[is_frame ? FRAME_ID : TILE_ID].chunkSize;
		  *data = (byte*)malloc(*size);
		  if (*data == null) return WEBP_MUX_MEMORY_ERROR;

		  // Fill in data according to frame/tile chunk format.
		  PutLE32(*data + 0, image_info.x_offset_);
		  PutLE32(*data + 4, image_info.y_offset_);

		  if (is_frame) {
			PutLE32(*data + 8, image_info.width_);
			PutLE32(*data + 12, image_info.height_);
			PutLE32(*data + 16, image_info.duration_);
		  }
		  return WEBP_MUX_OK;
		}

		// Outputs image data given data from a webp file (including RIFF header).
		static WebPMuxError GetImageData(byte* data, uint size, WebPData* image, WebPData* alpha)
		{
		  if (size < TAG_SIZE || memcmp(data, "RIFF", TAG_SIZE)) {
			// It is NOT webp file data. Return input data as is.
			image.bytes_ = data;
			image.size_ = size;
			return WEBP_MUX_OK;
		  } else {
			// It is webp file data. Extract image data from it.
			WebPMuxError err;
			WebPMuxState mux_state;
			WebPMux* mux = WebPMuxCreate(data, size, 0, &mux_state);
			if (mux == null || mux_state != WEBP_MUX_STATE_COMPLETE) {
			  return WEBP_MUX_BAD_DATA;
			}

			err = WebPMuxGetImage(mux, image, alpha);
			WebPMuxDelete(mux);
			return err;
		  }
		}

		static WebPMuxError DeleteChunks(WebPChunk** chunk_list, uint tag) {
		  WebPMuxError err = WEBP_MUX_NOT_FOUND;
		  assert(chunk_list);
		  while (*chunk_list) {
			WebPChunk* chunk = *chunk_list;
			if (chunk.tag_ == tag) {
			  *chunk_list = ChunkDelete(chunk);
			  err = WEBP_MUX_OK;
			} else {
			  chunk_list = &chunk.next_;
			}
		  }
		  return err;
		}

		static WebPMuxError MuxDeleteAllNamedData(WebPMux* mux, char* tag) {
		  TAG_ID id;
		  WebPChunk** chunk_list;

		  if (mux == null || tag == null) return WEBP_MUX_INVALID_ARGUMENT;

		  id = ChunkGetIdFromName(tag);
		  if (IsWPI(id)) return WEBP_MUX_INVALID_ARGUMENT;

		  chunk_list = GetChunkListFromId(mux, id);
		  if (chunk_list == null) return WEBP_MUX_INVALID_ARGUMENT;

		  return DeleteChunks(chunk_list, kChunks[id].chunkTag);
		}

		static WebPMuxError DeleteLoopCount(WebPMux* mux) {
		  return MuxDeleteAllNamedData(mux, kChunks[LOOP_ID].chunkName);
		}

		//------------------------------------------------------------------------------
		// Set API(s).

		// Sets the image in the mux object. Any existing images (including frame/tile)
		// will be removed.
		// Parameters:
		//   mux - (in/out) object in which the image is to be set
		//   data - (in) the image data to be set. The data can be either a VP8
		//          bitstream or a single-image WebP file (non-animated & non-tiled)
		//   size - (in) size of the image data
		//   alpha_data - (in) the alpha data corresponding to the image (if present)
		//   alpha_size - (in) size of alpha chunk data
		//   copy_data - (in) value 1 indicates given data WILL copied to the mux, and
		//               value 0 indicates data will NOT be copied.
		// Returns:
		//   WEBP_MUX_INVALID_ARGUMENT - if mux is null or data is null.
		//   WEBP_MUX_MEMORY_ERROR - on memory allocation error.
		//   WEBP_MUX_OK - on success.
		public WebPMuxError WebPMuxSetImage(WebPMux* mux, byte* data, uint size, byte* alpha_data, uint alpha_size, int copy_data)
		{
		  WebPMuxError err;
		  WebPChunk chunk;
		  WebPMuxImage wpi;
		  WebPData image;
		  int has_alpha = (alpha_data != null && alpha_size != 0);

		  if (mux == null || data == null || size > MAX_CHUNK_PAYLOAD) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  // If given data is for a whole webp file, extract only the VP8 data from it.
		  err = GetImageData(data, size, &image, null);
		  if (err != WEBP_MUX_OK) return err;

		  // Delete the existing images.
		  MuxImageDeleteAll(&mux.images_);

		  MuxImageInit(&wpi);

		  if (has_alpha) {  // Add alpha chunk.
			ChunkInit(&chunk);
			err = ChunkAssignDataImageInfo(&chunk, alpha_data, alpha_size, null,
										   copy_data, kChunks[ALPHA_ID].chunkTag);
			if (err != WEBP_MUX_OK) return err;
			err = ChunkSetNth(&chunk, &wpi.alpha_, 1);
			if (err != WEBP_MUX_OK) return err;
		  }

		  // Add image chunk.
		  ChunkInit(&chunk);
		  err = ChunkAssignDataImageInfo(&chunk, image.bytes_, image.size_, null,
										 copy_data, kChunks[IMAGE_ID].chunkTag);
		  if (err != WEBP_MUX_OK) return err;
		  err = ChunkSetNth(&chunk, &wpi.vp8_, 1);
		  if (err != WEBP_MUX_OK) return err;

		  // Add this image to mux.
		  return MuxImageSetNth(&wpi, &mux.images_, 1);
		}

		// Sets the XMP metadata in the mux object. Any existing metadata chunk(s) will
		// be removed.
		// Parameters:
		//   mux - (in/out) object to which the XMP metadata is to be added
		//   data - (in) the XMP metadata data to be added
		//   size - (in) size of the XMP metadata data
		//   copy_data - (in) value 1 indicates given data WILL copied to the mux, and
		//               value 0 indicates data will NOT be copied.
		// Returns:
		//   WEBP_MUX_INVALID_ARGUMENT - if mux is null or data is null.
		//   WEBP_MUX_MEMORY_ERROR - on memory allocation error.
		//   WEBP_MUX_OK - on success.
		public WebPMuxError WebPMuxSetMetadata(WebPMux* mux, byte* data, uint size, int copy_data) {
		  WebPMuxError err;

		  if (mux == null || data == null || size > MAX_CHUNK_PAYLOAD) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  // Delete the existing metadata chunk(s).
		  err = WebPMuxDeleteMetadata(mux);
		  if (err != WEBP_MUX_OK && err != WEBP_MUX_NOT_FOUND) return err;

		  // Add the given metadata chunk.
		  return MuxSet(mux, META_ID, 1, data, size, null, copy_data);
		}

		// Sets the color profile in the mux object. Any existing color profile chunk(s)
		// will be removed.
		// Parameters:
		//   mux - (in/out) object to which the color profile is to be added
		//   data - (in) the color profile data to be added
		//   size - (in) size of the color profile data
		//   copy_data - (in) value 1 indicates given data WILL copied to the mux, and
		//               value 0 indicates data will NOT be copied.
		// Returns:
		//   WEBP_MUX_INVALID_ARGUMENT - if mux is null or data is null
		//   WEBP_MUX_MEMORY_ERROR - on memory allocation error
		//   WEBP_MUX_OK - on success
		WebPMuxError WebPMuxSetColorProfile(WebPMux* mux, byte* data, uint size, int copy_data) {
		  WebPMuxError err;

		  if (mux == null || data == null || size > MAX_CHUNK_PAYLOAD) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  // Delete the existing ICCP chunk(s).
		  err = WebPMuxDeleteColorProfile(mux);
		  if (err != WEBP_MUX_OK && err != WEBP_MUX_NOT_FOUND) return err;

		  // Add the given ICCP chunk.
		  return MuxSet(mux, ICCP_ID, 1, data, size, null, copy_data);
		}

		// Sets the animation loop count in the mux object. Any existing loop count
		// value(s) will be removed.
		// Parameters:
		//   mux - (in/out) object in which loop chunk is to be set/added
		//   loop_count - (in) animation loop count value.
		//                Note that loop_count of zero denotes infinite loop.
		// Returns:
		//   WEBP_MUX_INVALID_ARGUMENT - if mux is null
		//   WEBP_MUX_MEMORY_ERROR - on memory allocation error.
		//   WEBP_MUX_OK - on success.
		WebPMuxError WebPMuxSetLoopCount(WebPMux* mux, uint loop_count) {
		  WebPMuxError err;
		  byte* data = null;

		  if (mux == null) return WEBP_MUX_INVALID_ARGUMENT;

		  // Delete the existing LOOP chunk(s).
		  err = DeleteLoopCount(mux);
		  if (err != WEBP_MUX_OK && err != WEBP_MUX_NOT_FOUND) return err;

		  // Add the given loop count.
		  data = (byte*)malloc(kChunks[LOOP_ID].chunkSize);
		  if (data == null) return WEBP_MUX_MEMORY_ERROR;

		  PutLE32(data, loop_count);
		  err = MuxAddChunk(mux, 1, kChunks[LOOP_ID].chunkTag, data,
							kChunks[LOOP_ID].chunkSize, null, 1);
		  free(data);
		  return err;
		}


		static WebPMuxError MuxAddFrameTileInternal(WebPMux* mux, uint nth,
													byte* data, uint size,
													byte* alpha_data,
													uint alpha_size,
													uint x_offset,
													uint y_offset,
													uint duration,
													int copy_data, uint tag) {
		  WebPChunk chunk;
		  WebPData image;
		  WebPMuxImage wpi;
		  WebPMuxError err;
		  WebPImageInfo* image_info = null;
		  byte* frame_tile_data = null;
		  uint frame_tile_data_size = 0;
		  int is_frame = (tag == kChunks[FRAME_ID].chunkTag) ? 1 : 0;
		  int has_alpha = (alpha_data != null && alpha_size != 0);

		  if (mux == null || data == null || size > MAX_CHUNK_PAYLOAD) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  // If given data is for a whole webp file, extract only the VP8 data from it.
		  err = GetImageData(data, size, &image, null);
		  if (err != WEBP_MUX_OK) return err;

		  ChunkInit(&chunk);
		  MuxImageInit(&wpi);

		  if (has_alpha) {
			// Add alpha chunk.
			err = ChunkAssignDataImageInfo(&chunk, alpha_data, alpha_size, null,
										   copy_data, kChunks[ALPHA_ID].chunkTag);
			if (err != WEBP_MUX_OK) return err;
			err = ChunkSetNth(&chunk, &wpi.alpha_, 1);
			if (err != WEBP_MUX_OK) return err;
			ChunkInit(&chunk);  // chunk owned by wpi.alpha_ now.
		  }

		  // Create image_info object.
		  image_info = CreateImageInfo(x_offset, y_offset, duration,
									   image.bytes_, image.size_);
		  if (image_info == null) {
			MuxImageRelease(&wpi);
			return WEBP_MUX_MEMORY_ERROR;
		  }

		  // Add image chunk.
		  err = ChunkAssignDataImageInfo(&chunk, image.bytes_, image.size_, image_info,
										 copy_data, kChunks[IMAGE_ID].chunkTag);
		  if (err != WEBP_MUX_OK) goto Err;
		  image_info = null;  // Owned by 'chunk' now.
		  err = ChunkSetNth(&chunk, &wpi.vp8_, 1);
		  if (err != WEBP_MUX_OK) goto Err;
		  ChunkInit(&chunk);  // chunk owned by wpi.vp8_ now.

		  // Create frame/tile data from image_info.
		  err = CreateDataFromImageInfo(wpi.vp8_.image_info_, is_frame,
										&frame_tile_data, &frame_tile_data_size);
		  if (err != WEBP_MUX_OK) goto Err;

		  // Add frame/tile chunk (with copy_data = 1).
		  err = ChunkAssignDataImageInfo(&chunk, frame_tile_data, frame_tile_data_size,
										 null, 1, tag);
		  if (err != WEBP_MUX_OK) goto Err;
		  free(frame_tile_data);
		  frame_tile_data = null;
		  err = ChunkSetNth(&chunk, &wpi.header_, 1);
		  if (err != WEBP_MUX_OK) goto Err;
		  ChunkInit(&chunk);  // chunk owned by wpi.header_ now.

		  // Add this WebPMuxImage to mux.
		  err = MuxImageSetNth(&wpi, &mux.images_, nth);
		  if (err != WEBP_MUX_OK) goto Err;

		  // All is well.
		  return WEBP_MUX_OK;

		 Err:  // Something bad happened.
		  free(image_info);
		  free(frame_tile_data);
		  ChunkRelease(&chunk);
		  MuxImageRelease(&wpi);
		  return err;
		}

		// TODO(urvang): Think about whether we need 'nth' while adding a frame or tile.

		// Adds an animation frame to the mux object.
		// nth=0 has a special meaning - last position.
		// Parameters:
		//   mux - (in/out) object to which an animation frame is to be added
		//   nth - (in) The position at which the frame is to be added.
		//   data - (in) the raw VP8 image data corresponding to frame image. The data
		//          can be either a VP8 bitstream or a single-image WebP file
		//          (non-animated & non-tiled)
		//   size - (in) size of frame chunk data
		//   alpha_data - (in) the alpha data corresponding to frame image (if present)
		//   alpha_size - (in) size of alpha chunk data
		//   x_offset - (in) x-offset of the frame to be added
		//   y_offset - (in) y-offset of the frame to be added
		//   duration - (in) duration of the frame to be added (in milliseconds)
		//   copy_data - (in) value 1 indicates given data WILL copied to the mux, and
		//               value 0 indicates data will NOT be copied.
		// Returns:
		//   WEBP_MUX_INVALID_ARGUMENT - if mux is null or data is null
		//   WEBP_MUX_NOT_FOUND - If we have less than (nth-1) frames before adding.
		//   WEBP_MUX_MEMORY_ERROR - on memory allocation error.
		//   WEBP_MUX_OK - on success.
		WebPMuxError WebPMuxAddFrame(WebPMux* mux, uint nth, byte* data, uint size, byte* alpha_data, uint alpha_size, uint x_offset, uint y_offset, uint duration, int copy_data) {
		  return MuxAddFrameTileInternal(mux, nth, data, size, alpha_data, alpha_size,
										 x_offset, y_offset, duration,
										 copy_data, kChunks[FRAME_ID].chunkTag);
		}

		// Adds a tile to the mux object.
		// nth=0 has a special meaning - last position.
		// Parameters:
		//   mux - (in/out) object to which a tile is to be added
		//   nth - (in) The position at which the tile is to be added.
		//   data - (in) the raw VP8 image data corresponding to tile image.  The data
		//          can be either a VP8 bitstream or a single-image WebP file
		//          (non-animated & non-tiled)
		//   size - (in) size of tile chunk data
		//   alpha_data - (in) the alpha data corresponding to tile image (if present)
		//   alpha_size - (in) size of alpha chunk data
		//   x_offset - (in) x-offset of the tile to be added
		//   y_offset - (in) y-offset of the tile to be added
		//   copy_data - (in) value 1 indicates given data WILL copied to the mux, and
		//               value 0 indicates data will NOT be copied.
		// Returns:
		//   WEBP_MUX_INVALID_ARGUMENT - if mux is null or data is null
		//   WEBP_MUX_NOT_FOUND - If we have less than (nth-1) tiles before adding.
		//   WEBP_MUX_MEMORY_ERROR - on memory allocation error.
		//   WEBP_MUX_OK - on success.
		WebPMuxError WebPMuxAddTile(WebPMux* mux, uint nth, byte* data, uint size, byte* alpha_data, uint alpha_size, uint x_offset, uint y_offset, int copy_data) {
		  return MuxAddFrameTileInternal(mux, nth, data, size, alpha_data, alpha_size,
										 x_offset, y_offset, 1,
										 copy_data, kChunks[TILE_ID].chunkTag);
		}

		//------------------------------------------------------------------------------
		// Delete API(s).

		// Deletes the image in the mux object.
		// Parameters:
		//   mux - (in/out) object from which the image is to be deleted
		// Returns:
		//   WEBP_MUX_INVALID_ARGUMENT - if mux is null
		//                               OR if mux contains animation/tiling.
		//   WEBP_MUX_NOT_FOUND - if image is not present in mux object.
		//   WEBP_MUX_OK - on success.
		WebPMuxError WebPMuxDeleteImage(WebPMux* mux) {
		  WebPMuxError err;

		  if (mux == null) return WEBP_MUX_INVALID_ARGUMENT;

		  err = ValidateForImage(mux);
		  if (err != WEBP_MUX_OK) return err;

		  // All Well, delete Image.
		  MuxImageDeleteAll(&mux.images_);
		  return WEBP_MUX_OK;
		}

		// Deletes the XMP metadata in the mux object.
		// Parameters:
		//   mux - (in/out) object from which XMP metadata is to be deleted
		// Returns:
		//   WEBP_MUX_INVALID_ARGUMENT - if mux is null
		//   WEBP_MUX_NOT_FOUND - If mux does not contain metadata.
		//   WEBP_MUX_OK - on success.
		WebPMuxError WebPMuxDeleteMetadata(WebPMux* mux) {
		  return MuxDeleteAllNamedData(mux, kChunks[META_ID].chunkName);
		}

		// Deletes the color profile in the mux object.
		// Parameters:
		//   mux - (in/out) object from which color profile is to be deleted
		// Returns:
		//   WEBP_MUX_INVALID_ARGUMENT - if mux is null
		//   WEBP_MUX_NOT_FOUND - If mux does not contain color profile.
		//   WEBP_MUX_OK - on success.
		WebPMuxError WebPMuxDeleteColorProfile(WebPMux* mux) {
		  return MuxDeleteAllNamedData(mux, kChunks[ICCP_ID].chunkName);
		}

		static WebPMuxError DeleteFrameTileInternal(WebPMux* mux,
													uint nth,
													char* tag) {
		  TAG_ID id;
		  if (mux == null) return WEBP_MUX_INVALID_ARGUMENT;

		  id = ChunkGetIdFromName(tag);
		  assert(id == FRAME_ID || id == TILE_ID);
		  return MuxImageDeleteNth(&mux.images_, nth, id);
		}

		// Deletes an animation frame from the mux object.
		// nth=0 has a special meaning - last position.
		// Parameters:
		//   mux - (in/out) object from which a frame is to be deleted
		//   nth - (in) The position from which the frame is to be deleted
		// Returns:
		//   WEBP_MUX_INVALID_ARGUMENT - if mux is null
		//   WEBP_MUX_NOT_FOUND - If there are less than nth frames in the mux object
		//                        before deletion.
		//   WEBP_MUX_OK - on success.
		WebPMuxError WebPMuxDeleteFrame(WebPMux* mux, uint nth) {
		  return DeleteFrameTileInternal(mux, nth, kChunks[FRAME_ID].chunkName);
		}

		// Deletes a tile from the mux object.
		// nth=0 has a special meaning - last position
		// Parameters:
		//   mux - (in/out) object from which a tile is to be deleted
		//   nth - (in) The position from which the tile is to be deleted
		// Returns:
		//   WEBP_MUX_INVALID_ARGUMENT - if mux is null
		//   WEBP_MUX_NOT_FOUND - If there are less than nth tiles in the mux object
		//                        before deletion.
		//   WEBP_MUX_OK - on success.
		WebPMuxError WebPMuxDeleteTile(WebPMux* mux, uint nth) {
		  return DeleteFrameTileInternal(mux, nth, kChunks[TILE_ID].chunkName);
		}

		//------------------------------------------------------------------------------
		// Assembly of the WebP RIFF file.

		static WebPMuxError GetImageCanvasHeightWidth(WebPMux* mux,
													  uint flags,
													  uint* width,
													  uint* height) {
		  uint max_x = 0;
		  uint max_y = 0;
		  ulong image_area = 0;
		  WebPMuxImage* wpi = null;

		  assert(mux != null);
		  assert(width && height);

		  wpi = mux.images_;
		  assert(wpi != null);
		  assert(wpi.vp8_ != null);

		  if (wpi.next_) {
			// Aggregate the bounding box for animation frames & tiled images.
			for (; wpi != null; wpi = wpi.next_) {
			  WebPImageInfo* image_info = wpi.vp8_.image_info_;

			  if (image_info != null) {
				uint max_x_pos = image_info.x_offset_ + image_info.width_;
				uint max_y_pos = image_info.y_offset_ + image_info.height_;
				if (max_x_pos < image_info.x_offset_) {  // Overflow occurred.
				  return WEBP_MUX_INVALID_ARGUMENT;
				}
				if (max_y_pos < image_info.y_offset_) {  // Overflow occurred.
				  return WEBP_MUX_INVALID_ARGUMENT;
				}
				if (max_x_pos > max_x) max_x = max_x_pos;
				if (max_y_pos > max_y) max_y = max_y_pos;
				image_area += (image_info.width_ * image_info.height_);
			  }
			}
			*width = max_x;
			*height = max_y;
			// Crude check to validate that there are no image overlaps/holes for tile
			// images. Check that the aggregated image area for individual tiles exactly
			// matches the image area of the constructed canvas. However, the area-match
			// is necessary but not sufficient condition.
			if ((flags & TILE_FLAG) && (image_area != (max_x * max_y))) {
			  *width = 0;
			  *height = 0;
			  return WEBP_MUX_INVALID_ARGUMENT;
			}
		  } else {
			// For a single image, extract the width & height from VP8 image-data.
			int w, h;
			WebPChunk* image_chunk = wpi.vp8_;
			assert(image_chunk != null);
			if (VP8GetInfo(image_chunk.data_, image_chunk.payload_size_,
						   image_chunk.payload_size_, &w, &h)) {
			  *width = w;
			  *height = h;
			}
		  }
		  return WEBP_MUX_OK;
		}

		// VP8X format:
		// Total Size : 12,
		// Flags  : 4 bytes,
		// Width  : 4 bytes,
		// Height : 4 bytes.
		static WebPMuxError CreateVP8XChunk(WebPMux* mux) {
		  WebPMuxError err = WEBP_MUX_OK;
		  uint flags = 0;
		  uint width = 0;
		  uint height = 0;
		  byte data[VP8X_CHUNK_SIZE];
		  uint data_size = VP8X_CHUNK_SIZE;
		  WebPMuxImage* images = null;

		  images = mux.images_;  // First image.

		  assert(mux != null);
		  if (images == null || images.vp8_ == null || images.vp8_.data_ == null) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  // If VP8X chunk(s) is(are) already present, remove them (and later add new
		  // VP8X chunk with updated flags).
		  err = MuxDeleteAllNamedData(mux, kChunks[VP8X_ID].chunkName);
		  if (err != WEBP_MUX_OK && err != WEBP_MUX_NOT_FOUND) return err;

		  // Set flags.
		  if (mux.iccp_ != null && mux.iccp_.data_ != null) {
			flags |= ICCP_FLAG;
		  }

		  if (mux.meta_ != null && mux.meta_.data_ != null) {
			flags |= META_FLAG;
		  }

		  if (images.header_ != null) {
			if (images.header_.tag_ == kChunks[TILE_ID].chunkTag) {
			  // This is a tiled image.
			  flags |= TILE_FLAG;
			} else if (images.header_.tag_ == kChunks[FRAME_ID].chunkTag) {
			  // This is an image with animation.
			  flags |= ANIMATION_FLAG;
			}
		  }

		  if (images.alpha_ != null && images.alpha_.data_ != null) {
			// This is an image with alpha channel.
			flags |= ALPHA_FLAG;
		  }

		  if (flags == 0) {
			// For Simple Image, VP8X chunk should not be added.
			return WEBP_MUX_OK;
		  }

		  err = GetImageCanvasHeightWidth(mux, flags, &width, &height);
		  if (err != WEBP_MUX_OK) return err;

		  PutLE32(data + 0, flags);   // Put VP8X Chunk Flags.
		  PutLE32(data + 4, width);   // Put canvasWidth.
		  PutLE32(data + 8, height);  // Put canvasHeight.

		  err = MuxAddChunk(mux, 1, kChunks[VP8X_ID].chunkTag, data, data_size,
							null, 1);
		  return err;
		}

		// Assembles all chunks in WebP RIFF format and returns in output_data.
		// This function also validates the mux object.
		// The content of '*output_data' is allocated using malloc(), and NOT
		// owned by the 'mux' object.
		// It MUST be deallocated by the caller by calling free().
		// Parameters:
		//   mux - (in/out) object whose chunks are to be assembled
		//   output_data - (out) byte array where assembled WebP data is returned
		//   output_size - (out) size of returned data
		// Returns:
		//   WEBP_MUX_BAD_DATA - if mux object is invalid.
		//   WEBP_MUX_INVALID_ARGUMENT - if either mux, output_data or output_size is
		//                               null.
		//   WEBP_MUX_MEMORY_ERROR - on memory allocation error.
		//   WEBP_MUX_OK - on success
		public WebPMuxError WebPMuxAssemble(WebPMux* mux, byte** output_data, uint* output_size) {
		  uint size = 0;
		  byte* data = null;
		  byte* dst = null;
		  int num_frames;
		  int num_loop_chunks;
		  WebPMuxError err;

		  if (mux == null || output_data == null || output_size == null) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  *output_data = null;
		  *output_size = 0;

		  // Remove LOOP chunk if unnecessary.
		  err = WebPMuxNumNamedElements(mux, kChunks[LOOP_ID].chunkName,
										&num_loop_chunks);
		  if (err != WEBP_MUX_OK) return err;
		  if (num_loop_chunks >= 1) {
			err = WebPMuxNumNamedElements(mux, kChunks[FRAME_ID].chunkName,
										  &num_frames);
			if (err != WEBP_MUX_OK) return err;
			if (num_frames == 0) {
			  err = DeleteLoopCount(mux);
			  if (err != WEBP_MUX_OK) return err;
			}
		  }

		  // Create VP8X chunk.
		  err = CreateVP8XChunk(mux);
		  if (err != WEBP_MUX_OK) return err;

		  // Mark mux as complete.
		  mux.state_ = WEBP_MUX_STATE_COMPLETE;

		  // Allocate data.
		  size = ChunksListDiskSize(mux.vp8x_) + ChunksListDiskSize(mux.iccp_)
			   + ChunksListDiskSize(mux.loop_) + MuxImageListDiskSize(mux.images_)
			   + ChunksListDiskSize(mux.meta_) + ChunksListDiskSize(mux.unknown_)
			   + RIFF_HEADER_SIZE;

		  data = (byte*)malloc(size);
		  if (data == null) return WEBP_MUX_MEMORY_ERROR;

		  // Main RIFF header.
		  PutLE32(data + 0, mktag('R', 'I', 'F', 'F'));
		  PutLE32(data + 4, size - CHUNK_HEADER_SIZE);
		  PutLE32(data + 8, mktag('W', 'E', 'B', 'P'));

		  // Chunks.
		  dst = data + RIFF_HEADER_SIZE;
		  dst = ChunkListEmit(mux.vp8x_, dst);
		  dst = ChunkListEmit(mux.iccp_, dst);
		  dst = ChunkListEmit(mux.loop_, dst);
		  dst = MuxImageListEmit(mux.images_, dst);
		  dst = ChunkListEmit(mux.meta_, dst);
		  dst = ChunkListEmit(mux.unknown_, dst);
		  assert(dst == data + size);

		  // Validate mux.
		  err = WebPMuxValidate(mux);
		  if (err != WEBP_MUX_OK) {
			free(data);
			data = null;
			size = 0;
		  }

		  // Finalize.
		  *output_data = data;
		  *output_size = size;

		  return err;
		}

		//------------------------------------------------------------------------------


	}
}
#endif

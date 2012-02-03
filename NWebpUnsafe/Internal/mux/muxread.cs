using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if false
namespace NWebp.Internal
{
	class muxread
	{
		//------------------------------------------------------------------------------
		// Helper method(s).

		// Handy MACRO.
		void SWITCH_ID_LIST(ID, LIST) {
		  if (id == (ID)) {                                               
			WebPChunk* chunk = ChunkSearchList((LIST), nth,               
														   kChunks[(ID)].chunkTag); 
			if (chunk) {                                                            
			  data.bytes_ = chunk.data_;                                            
			  data.size_ = chunk.payload_size_;                                     
			  return WEBP_MUX_OK;                                                    
			} else {                                                                 
			  return WEBP_MUX_NOT_FOUND;                                             
			}                                                                        
		  }
		}

		static WebPMuxError MuxGet(WebPMux* mux, TAG_ID id, uint nth,
								   WebPData* data) {
		  assert(mux != null);
		  memset(data, 0, sizeof(*data));
		  assert(!IsWPI(id));

		  SWITCH_ID_LIST(VP8X_ID, mux.vp8x_);
		  SWITCH_ID_LIST(ICCP_ID, mux.iccp_);
		  SWITCH_ID_LIST(LOOP_ID, mux.loop_);
		  SWITCH_ID_LIST(META_ID, mux.meta_);
		  SWITCH_ID_LIST(UNKNOWN_ID, mux.unknown_);
		  return WEBP_MUX_NOT_FOUND;
		}
		#undef SWITCH_ID_LIST

		// Fill the chunk with the given data, after verifying that the data size
		// doesn't exceed 'max_size'.
		static WebPMuxError ChunkAssignData(WebPChunk* chunk, byte* data,
											uint data_size, uint riff_size,
											int copy_data) {
		  uint chunk_size;

		  // Sanity checks.
		  if (data_size < TAG_SIZE) return WEBP_MUX_NOT_ENOUGH_DATA;
		  chunk_size = GetLE32(data + TAG_SIZE);

		  {
			uint chunk_disk_size = SizeWithPadding(chunk_size);
			if (chunk_disk_size > riff_size) return WEBP_MUX_BAD_DATA;
			if (chunk_disk_size > data_size) return WEBP_MUX_NOT_ENOUGH_DATA;
		  }

		  // Data assignment.
		  return ChunkAssignDataImageInfo(chunk, data + CHUNK_HEADER_SIZE, chunk_size,
										  null, copy_data, GetLE32(data + 0));
		}

		//------------------------------------------------------------------------------
		// Create a mux object from WebP-RIFF data.

		// Creates a mux object from raw data given in WebP RIFF format.
		// Parameters:
		//   data - (in) the raw data in WebP RIFF format
		//   size - (in) size of raw data
		//   copy_data - (in) value 1 indicates given data WILL copied to the mux, and
		//               value 0 indicates data will NOT be copied.
		//   mux_state - (out) indicates the state of the mux returned. Can be passed
		//               null if not required.
		// Returns:
		//   A pointer to the mux object created from given data - on success.
		//   null - In case of invalid data or memory error.
		public WebPMux* WebPMuxCreate(byte* data, uint size, int copy_data,
							   WebPMuxState* mux_state) {
		  uint riff_size;
		  uint tag;
		  byte* end;
		  TAG_ID id;
		  WebPMux* mux = null;
		  WebPMuxImage* wpi = null;

		  if (mux_state) *mux_state = WEBP_MUX_STATE_PARTIAL;

		  // Sanity checks.
		  if (data == null) goto Err;
		  if (size < RIFF_HEADER_SIZE) return null;
		  if (GetLE32(data + 0) != mktag('R', 'I', 'F', 'F') ||
			  GetLE32(data + CHUNK_HEADER_SIZE) != mktag('W', 'E', 'B', 'P')) {
			goto Err;
		  }

		  mux = WebPMuxNew();
		  if (mux == null) goto Err;

		  if (size < RIFF_HEADER_SIZE + TAG_SIZE) {
			mux.state_ = WEBP_MUX_STATE_PARTIAL;
			goto Ok;
		  }

		  tag = GetLE32(data + RIFF_HEADER_SIZE);
		  if (tag != kChunks[IMAGE_ID].chunkTag && tag != kChunks[VP8X_ID].chunkTag) {
			// First chunk should be either VP8X or VP8.
			goto Err;
		  }

		  riff_size = SizeWithPadding(GetLE32(data + TAG_SIZE));
		  if (riff_size > MAX_CHUNK_PAYLOAD) {
			goto Err;
		  } else if (riff_size > size) {
			mux.state_ = WEBP_MUX_STATE_PARTIAL;
		  } else {
			mux.state_ = WEBP_MUX_STATE_COMPLETE;
			if (riff_size < size) {  // Redundant data after last chunk.
			  size = riff_size;  // To make sure we don't read any data beyond mux_size.
			}
		  }

		  end = data + size;
		  data += RIFF_HEADER_SIZE;
		  size -= RIFF_HEADER_SIZE;

		  wpi = (WebPMuxImage*)malloc(sizeof(*wpi));
		  if (wpi == null) goto Err;
		  MuxImageInit(wpi);

		  // Loop over chunks.
		  while (data != end) {
			WebPChunk chunk;
			WebPMuxError err;

			ChunkInit(&chunk);
			err = ChunkAssignData(&chunk, data, size, riff_size, copy_data);
			if (err != WEBP_MUX_OK) {
			  if (err == WEBP_MUX_NOT_ENOUGH_DATA &&
				  mux.state_ == WEBP_MUX_STATE_PARTIAL) {
				goto Ok;
			  } else {
				goto Err;
			  }
			}

			id = ChunkGetIdFromTag(chunk.tag_);

			if (IsWPI(id)) {  // An image chunk (frame/tile/alpha/vp8).
			  WebPChunk** wpi_chunk_ptr;
			  wpi_chunk_ptr = MuxImageGetListFromId(wpi, id);  // Image chunk to set.
			  assert(wpi_chunk_ptr != null);
			  if (*wpi_chunk_ptr != null) goto Err;  // Consecutive alpha chunks or
													 // consecutive frame/tile chunks.
			  if (ChunkSetNth(&chunk, wpi_chunk_ptr, 1) != WEBP_MUX_OK) goto Err;
			  if (id == IMAGE_ID) {
				wpi.is_partial_ = 0;  // wpi is completely filled.
				// Add this to mux.images_ list.
				if (MuxImageSetNth(wpi, &mux.images_, 0) != WEBP_MUX_OK) goto Err;
				MuxImageInit(wpi);  // Reset for reading next image.
			  } else {
				wpi.is_partial_ = 1;  // wpi is only partially filled.
			  }
			} else {  // A non-image chunk.
			  WebPChunk** chunk_list;
			  if (wpi.is_partial_) goto Err;  // Encountered a non-image chunk before
											   // getting all chunks of an image.
			  chunk_list = GetChunkListFromId(mux, id);  // List for adding this chunk.
			  if (chunk_list == null) chunk_list = (WebPChunk**)&mux.unknown_;
			  if (ChunkSetNth(&chunk, chunk_list, 0) != WEBP_MUX_OK) goto Err;
			}

			{
			  uint data_size = ChunkDiskSize(&chunk);
			  data += data_size;
			  size -= data_size;
			}
		  }

		  // Validate mux if complete.
		  if (WebPMuxValidate(mux) != WEBP_MUX_OK) goto Err;

		 Ok:
		  MuxImageDelete(wpi);
		  if (mux_state) *mux_state = mux.state_;
		  return mux;  // All OK;

		 Err:  // Something bad happened.
		  MuxImageDelete(wpi);
		  WebPMuxDelete(mux);
		  if (mux_state) *mux_state = WEBP_MUX_STATE_ERROR;
		  return null;
		}

		//------------------------------------------------------------------------------
		// Get API(s).

		// Gets the feature flags from the mux object.
		// Parameters:
		//   mux - (in) object from which the features are to be fetched
		//   flags - (out) the flags specifying which features are present in the
		//           mux object. This will be an OR of various flag values.
		//           Enum 'FeatureFlags' can be used to test for individual flag values.
		// Returns:
		//   WEBP_MUX_INVALID_ARGUMENT - if mux is null or flags is null
		//   WEBP_MUX_NOT_FOUND - if VP8X chunk is not present in mux object.
		//   WEBP_MUX_BAD_DATA - if VP8X chunk in mux is invalid.
		//   WEBP_MUX_OK - on success.
		public WebPMuxError WebPMuxGetFeatures(WebPMux* mux, uint* flags) {
		  WebPData data;
		  WebPMuxError err;

		  if (mux == null || flags == null) return WEBP_MUX_INVALID_ARGUMENT;
		  *flags = 0;

		  // Check if VP8X chunk is present.
		  err = MuxGet(mux, VP8X_ID, 1, &data);
		  if (err == WEBP_MUX_NOT_FOUND) {
			// Check if VP8 chunk is present.
			err = WebPMuxGetImage(mux, &data, null);
			if (err == WEBP_MUX_NOT_FOUND &&              // Data not available (yet).
				mux.state_ == WEBP_MUX_STATE_PARTIAL) {  // Incremental case.
			  return WEBP_MUX_NOT_ENOUGH_DATA;
			} else {
			  return err;
			}
		  } else if (err != WEBP_MUX_OK) {
			return err;
		  }

		  // TODO(urvang): Add a '#define CHUNK_SIZE_BYTES 4' and use it instead of
		  // hard-coded value of 4 everywhere.
		  if (data.size_ < 4) return WEBP_MUX_BAD_DATA;

		  // All OK. Fill up flags.
		  *flags = GetLE32(data.bytes_);
		  return WEBP_MUX_OK;
		}

		// Gets a reference to the image in the mux object.
		// The caller should NOT free the returned data.
		// Parameters:
		//   mux - (in) object from which the image is to be fetched
		//   image - (out) the image data
		//   alpha - (out) the alpha data of the image (if present)
		// Returns:
		//   WEBP_MUX_INVALID_ARGUMENT - if either mux or image is null
		//                               OR if mux contains animation/tiling.
		//   WEBP_MUX_NOT_FOUND - if image is not present in mux object.
		//   WEBP_MUX_OK - on success.
		public WebPMuxError WebPMuxGetImage(WebPMux* mux, WebPData* image, WebPData* alpha) {
		  WebPMuxError err;
		  WebPMuxImage* wpi = null;

		  if (mux == null || image == null) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  memset(image, 0, sizeof(*image));

		  err = ValidateForImage(mux);
		  if (err != WEBP_MUX_OK) return err;

		  // All well. Get the image.
		  err = MuxImageGetNth((WebPMuxImage**)&mux.images_, 1, IMAGE_ID, &wpi);
		  assert(err == WEBP_MUX_OK);  // Already tested above.

		  // Get alpha chunk (if present & requested).
		  if (alpha != null) {
			memset(alpha, 0, sizeof(*alpha));
			if (wpi.alpha_ != null) {
			  alpha.bytes_ = wpi.alpha_.data_;
			  alpha.size_ = wpi.alpha_.payload_size_;
			}
		  }

		  // Get image chunk.
		  if (wpi.vp8_ != null) {
			image.bytes_ = wpi.vp8_.data_;
			image.size_ = wpi.vp8_.payload_size_;
		  }
		  return WEBP_MUX_OK;
		}

		// Gets a reference to the XMP metadata in the mux object.
		// The caller should NOT free the returned data.
		// Parameters:
		//   mux - (in) object from which the XMP metadata is to be fetched
		//   metadata - (out) XMP metadata
		// Returns:
		//   WEBP_MUX_INVALID_ARGUMENT - if either mux or metadata is null.
		//   WEBP_MUX_NOT_FOUND - if metadata is not present in mux object.
		//   WEBP_MUX_OK - on success.
		public WebPMuxError WebPMuxGetMetadata(WebPMux* mux, WebPData* metadata) {
		  if (mux == null || metadata == null) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  return MuxGet(mux, META_ID, 1, metadata);
		}

		// Gets a reference to the color profile in the mux object.
		// The caller should NOT free the returned data.
		// Parameters:
		//   mux - (in) object from which the color profile data is to be fetched
		//   color_profile - (out) color profile data
		// Returns:
		//   WEBP_MUX_INVALID_ARGUMENT - if either mux or color_profile is null.
		//   WEBP_MUX_NOT_FOUND - if color profile is not present in mux object.
		//   WEBP_MUX_OK - on success.
		WebPMuxError WebPMuxGetColorProfile(WebPMux* mux,
											WebPData* color_profile) {
		  if (mux == null || color_profile == null) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  return MuxGet(mux, ICCP_ID, 1, color_profile);
		}

		// Gets the animation loop count from the mux object.
		// Parameters:
		//   mux - (in) object from which the loop count is to be fetched
		//   loop_count - (out) the loop_count value present in the LOOP chunk
		// Returns:
		//   WEBP_MUX_INVALID_ARGUMENT - if either of mux or loop_count is null
		//   WEBP_MUX_NOT_FOUND - if loop chunk is not present in mux object.
		//   WEBP_MUX_OK - on success.
		WebPMuxError WebPMuxGetLoopCount(WebPMux* mux, uint* loop_count) {
		  WebPData image;
		  WebPMuxError err;

		  if (mux == null || loop_count == null) return WEBP_MUX_INVALID_ARGUMENT;

		  err = MuxGet(mux, LOOP_ID, 1, &image);
		  if (err != WEBP_MUX_OK) return err;
		  if (image.size_ < kChunks[LOOP_ID].chunkSize) return WEBP_MUX_BAD_DATA;
		  *loop_count = GetLE32(image.bytes_);

		  return WEBP_MUX_OK;
		}

		static WebPMuxError MuxGetFrameTileInternal(WebPMux* mux,
													uint nth,
													WebPData* image,
													WebPData* alpha,
													uint* x_offset,
													uint* y_offset,
													uint* duration, uint tag) {
		  byte* frame_tile_data;
		  uint frame_tile_size;
		  WebPMuxError err;
		  WebPMuxImage* wpi;

		  int is_frame = (tag == kChunks[FRAME_ID].chunkTag) ? 1 : 0;
		  TAG_ID id = is_frame ? FRAME_ID : TILE_ID;

		  if (mux == null || image == null ||
			  x_offset == null || y_offset == null || (is_frame && duration == null)) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  // Get the nth WebPMuxImage.
		  err = MuxImageGetNth((WebPMuxImage**)&mux.images_, nth, id, &wpi);
		  if (err != WEBP_MUX_OK) return err;

		  // Get frame chunk.
		  assert(wpi.header_ != null);  // As GetNthImage() already checked header_.
		  frame_tile_data = wpi.header_.data_;
		  frame_tile_size = wpi.header_.payload_size_;

		  if (frame_tile_size < kChunks[id].chunkSize) return WEBP_MUX_BAD_DATA;
		  *x_offset = GetLE32(frame_tile_data + 0);
		  *y_offset = GetLE32(frame_tile_data + 4);
		  if (is_frame) *duration = GetLE32(frame_tile_data + 16);

		  // Get alpha chunk (if present & requested).
		  if (alpha != null) {
			memset(alpha, 0, sizeof(*alpha));
			if (wpi.alpha_ != null) {
			  alpha.bytes_ = wpi.alpha_.data_;
			  alpha.size_ = wpi.alpha_.payload_size_;
			}
		  }

		  // Get image chunk.
		  memset(image, 0, sizeof(*image));
		  if (wpi.vp8_ != null) {
			image.bytes_ = wpi.vp8_.data_;
			image.size_ = wpi.vp8_.payload_size_;
		  }

		  return WEBP_MUX_OK;
		}

		// Gets a reference to the nth animation frame from the mux object.
		// The caller should NOT free the returned data.
		// nth=0 has a special meaning - last position.
		// Parameters:
		//   mux - (in) object from which the info is to be fetched
		//   nth - (in) index of the frame in the mux object
		//   image - (out) the image data
		//   alpha - (out) the alpha data corresponding to frame image (if present)
		//   x_offset - (out) x-offset of the returned frame
		//   y_offset - (out) y-offset of the returned frame
		//   duration - (out) duration of the returned frame (in milliseconds)
		// Returns:
		//   WEBP_MUX_INVALID_ARGUMENT - if either mux, image, x_offset,
		//                               y_offset, or duration is null
		//   WEBP_MUX_NOT_FOUND - if there are less than nth frames in the mux object.
		//   WEBP_MUX_BAD_DATA - if nth frame chunk in mux is invalid.
		//   WEBP_MUX_OK - on success.
		WebPMuxError WebPMuxGetFrame(WebPMux* mux, uint nth, WebPData* image, WebPData* alpha, uint* x_offset, uint* y_offset, uint* duration) {
		  return MuxGetFrameTileInternal(mux, nth, image, alpha,
										 x_offset, y_offset, duration,
										 kChunks[FRAME_ID].chunkTag);
		}

		// Gets a reference to the nth tile from the mux object.
		// The caller should NOT free the returned data.
		// nth=0 has a special meaning - last position.
		// Parameters:
		//   mux - (in) object from which the info is to be fetched
		//   nth - (in) index of the tile in the mux object
		//   image - (out) the image data
		//   alpha - (out) the alpha data corresponding to tile image (if present)
		//   x_offset - (out) x-offset of the returned tile
		//   y_offset - (out) y-offset of the returned tile
		// Returns:
		//   WEBP_MUX_INVALID_ARGUMENT - if either mux, image, x_offset or
		//                               y_offset is null
		//   WEBP_MUX_NOT_FOUND - if there are less than nth tiles in the mux object.
		//   WEBP_MUX_BAD_DATA - if nth tile chunk in mux is invalid.
		//   WEBP_MUX_OK - on success.
		WebPMuxError WebPMuxGetTile(WebPMux* mux, uint nth, WebPData* image, WebPData* alpha, uint* x_offset, uint* y_offset) {
		  return MuxGetFrameTileInternal(mux, nth, image, alpha,
										 x_offset, y_offset, null,
										 kChunks[TILE_ID].chunkTag);
		}

		// Count number of chunks matching 'tag' in the 'chunk_list'.
		// If tag == NIL_TAG, any tag will be matched.
		static int CountChunks(WebPChunk* chunk_list, uint tag) {
		  int count = 0;
		  WebPChunk* current;
		  for (current = chunk_list; current != null; current = current.next_) {
			if (tag == NIL_TAG || current.tag_ == tag) {
			  count++;  // Count chunks whose tags match.
			}
		  }
		  return count;
		}

		// Gets number of chunks having tag value tag in the mux object.
		// Parameters:
		//   mux - (in) object from which the info is to be fetched
		//   tag - (in) tag name specifying the type of chunk
		//   num_elements - (out) number of chunks corresponding to the specified tag
		// Returns:
		//   WEBP_MUX_INVALID_ARGUMENT - if either mux, tag or num_elements is null
		//   WEBP_MUX_OK - on success.
		public WebPMuxError WebPMuxNumNamedElements(WebPMux* mux, char* tag,
											 int* num_elements) {
		  TAG_ID id;
		  WebPChunk** chunk_list;

		  if (mux == null || tag == null || num_elements == null) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  id = ChunkGetIdFromName(tag);
		  if (IsWPI(id)) {
			*num_elements = MuxImageCount(mux.images_, id);
		  } else {
			chunk_list = GetChunkListFromId(mux, id);
			if (chunk_list == null) {
			  *num_elements = 0;
			} else {
			  *num_elements = CountChunks(*chunk_list, kChunks[id].chunkTag);
			}
		  }

		  return WEBP_MUX_OK;
		}

		//------------------------------------------------------------------------------
	}
}
#endif
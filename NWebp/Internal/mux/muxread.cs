﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal.mux
{
	class muxread
	{
		//------------------------------------------------------------------------------
		// Helper method(s).

		// Handy MACRO.
		#define SWITCH_ID_LIST(ID, LIST)                                              \
		  if (id == (ID)) {                                                           \
			const WebPChunk* const chunk = ChunkSearchList((LIST), nth,               \
														   kChunks[(ID)].chunkTag);   \
			if (chunk) {                                                              \
			  data->bytes_ = chunk->data_;                                            \
			  data->size_ = chunk->payload_size_;                                     \
			  return WEBP_MUX_OK;                                                     \
			} else {                                                                  \
			  return WEBP_MUX_NOT_FOUND;                                              \
			}                                                                         \
		  }

		static WebPMuxError MuxGet(const WebPMux* const mux, TAG_ID id, uint nth,
								   WebPData* const data) {
		  assert(mux != null);
		  memset(data, 0, sizeof(*data));
		  assert(!IsWPI(id));

		  SWITCH_ID_LIST(VP8X_ID, mux->vp8x_);
		  SWITCH_ID_LIST(ICCP_ID, mux->iccp_);
		  SWITCH_ID_LIST(LOOP_ID, mux->loop_);
		  SWITCH_ID_LIST(META_ID, mux->meta_);
		  SWITCH_ID_LIST(UNKNOWN_ID, mux->unknown_);
		  return WEBP_MUX_NOT_FOUND;
		}
		#undef SWITCH_ID_LIST

		// Fill the chunk with the given data, after verifying that the data size
		// doesn't exceed 'max_size'.
		static WebPMuxError ChunkAssignData(WebPChunk* chunk, const byte* data,
											uint data_size, uint riff_size,
											int copy_data) {
		  uint chunk_size;

		  // Sanity checks.
		  if (data_size < TAG_SIZE) return WEBP_MUX_NOT_ENOUGH_DATA;
		  chunk_size = GetLE32(data + TAG_SIZE);

		  {
			const uint chunk_disk_size = SizeWithPadding(chunk_size);
			if (chunk_disk_size > riff_size) return WEBP_MUX_BAD_DATA;
			if (chunk_disk_size > data_size) return WEBP_MUX_NOT_ENOUGH_DATA;
		  }

		  // Data assignment.
		  return ChunkAssignDataImageInfo(chunk, data + CHUNK_HEADER_SIZE, chunk_size,
										  null, copy_data, GetLE32(data + 0));
		}

		//------------------------------------------------------------------------------
		// Create a mux object from WebP-RIFF data.

		WebPMux* WebPMuxCreate(const byte* data, uint size, int copy_data,
							   WebPMuxState* const mux_state) {
		  uint riff_size;
		  uint tag;
		  const byte* end;
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
			mux->state_ = WEBP_MUX_STATE_PARTIAL;
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
			mux->state_ = WEBP_MUX_STATE_PARTIAL;
		  } else {
			mux->state_ = WEBP_MUX_STATE_COMPLETE;
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
				  mux->state_ == WEBP_MUX_STATE_PARTIAL) {
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
				wpi->is_partial_ = 0;  // wpi is completely filled.
				// Add this to mux->images_ list.
				if (MuxImageSetNth(wpi, &mux->images_, 0) != WEBP_MUX_OK) goto Err;
				MuxImageInit(wpi);  // Reset for reading next image.
			  } else {
				wpi->is_partial_ = 1;  // wpi is only partially filled.
			  }
			} else {  // A non-image chunk.
			  WebPChunk** chunk_list;
			  if (wpi->is_partial_) goto Err;  // Encountered a non-image chunk before
											   // getting all chunks of an image.
			  chunk_list = GetChunkListFromId(mux, id);  // List for adding this chunk.
			  if (chunk_list == null) chunk_list = (WebPChunk**)&mux->unknown_;
			  if (ChunkSetNth(&chunk, chunk_list, 0) != WEBP_MUX_OK) goto Err;
			}

			{
			  const uint data_size = ChunkDiskSize(&chunk);
			  data += data_size;
			  size -= data_size;
			}
		  }

		  // Validate mux if complete.
		  if (WebPMuxValidate(mux) != WEBP_MUX_OK) goto Err;

		 Ok:
		  MuxImageDelete(wpi);
		  if (mux_state) *mux_state = mux->state_;
		  return mux;  // All OK;

		 Err:  // Something bad happened.
		  MuxImageDelete(wpi);
		  WebPMuxDelete(mux);
		  if (mux_state) *mux_state = WEBP_MUX_STATE_ERROR;
		  return null;
		}

		//------------------------------------------------------------------------------
		// Get API(s).

		WebPMuxError WebPMuxGetFeatures(const WebPMux* const mux, uint* flags) {
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
				mux->state_ == WEBP_MUX_STATE_PARTIAL) {  // Incremental case.
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

		WebPMuxError WebPMuxGetImage(const WebPMux* const mux,
									 WebPData* const image, WebPData* const alpha) {
		  WebPMuxError err;
		  WebPMuxImage* wpi = null;

		  if (mux == null || image == null) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  memset(image, 0, sizeof(*image));

		  err = ValidateForImage(mux);
		  if (err != WEBP_MUX_OK) return err;

		  // All well. Get the image.
		  err = MuxImageGetNth((const WebPMuxImage**)&mux->images_, 1, IMAGE_ID, &wpi);
		  assert(err == WEBP_MUX_OK);  // Already tested above.

		  // Get alpha chunk (if present & requested).
		  if (alpha != null) {
			memset(alpha, 0, sizeof(*alpha));
			if (wpi->alpha_ != null) {
			  alpha->bytes_ = wpi->alpha_->data_;
			  alpha->size_ = wpi->alpha_->payload_size_;
			}
		  }

		  // Get image chunk.
		  if (wpi->vp8_ != null) {
			image->bytes_ = wpi->vp8_->data_;
			image->size_ = wpi->vp8_->payload_size_;
		  }
		  return WEBP_MUX_OK;
		}

		WebPMuxError WebPMuxGetMetadata(const WebPMux* const mux,
										WebPData* const metadata) {
		  if (mux == null || metadata == null) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  return MuxGet(mux, META_ID, 1, metadata);
		}

		WebPMuxError WebPMuxGetColorProfile(const WebPMux* const mux,
											WebPData* const color_profile) {
		  if (mux == null || color_profile == null) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  return MuxGet(mux, ICCP_ID, 1, color_profile);
		}

		WebPMuxError WebPMuxGetLoopCount(const WebPMux* const mux,
										 uint* loop_count) {
		  WebPData image;
		  WebPMuxError err;

		  if (mux == null || loop_count == null) return WEBP_MUX_INVALID_ARGUMENT;

		  err = MuxGet(mux, LOOP_ID, 1, &image);
		  if (err != WEBP_MUX_OK) return err;
		  if (image.size_ < kChunks[LOOP_ID].chunkSize) return WEBP_MUX_BAD_DATA;
		  *loop_count = GetLE32(image.bytes_);

		  return WEBP_MUX_OK;
		}

		static WebPMuxError MuxGetFrameTileInternal(const WebPMux* const mux,
													uint nth,
													WebPData* const image,
													WebPData* const alpha,
													uint* x_offset,
													uint* y_offset,
													uint* duration, uint tag) {
		  const byte* frame_tile_data;
		  uint frame_tile_size;
		  WebPMuxError err;
		  WebPMuxImage* wpi;

		  const int is_frame = (tag == kChunks[FRAME_ID].chunkTag) ? 1 : 0;
		  const TAG_ID id = is_frame ? FRAME_ID : TILE_ID;

		  if (mux == null || image == null ||
			  x_offset == null || y_offset == null || (is_frame && duration == null)) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  // Get the nth WebPMuxImage.
		  err = MuxImageGetNth((const WebPMuxImage**)&mux->images_, nth, id, &wpi);
		  if (err != WEBP_MUX_OK) return err;

		  // Get frame chunk.
		  assert(wpi->header_ != null);  // As GetNthImage() already checked header_.
		  frame_tile_data = wpi->header_->data_;
		  frame_tile_size = wpi->header_->payload_size_;

		  if (frame_tile_size < kChunks[id].chunkSize) return WEBP_MUX_BAD_DATA;
		  *x_offset = GetLE32(frame_tile_data + 0);
		  *y_offset = GetLE32(frame_tile_data + 4);
		  if (is_frame) *duration = GetLE32(frame_tile_data + 16);

		  // Get alpha chunk (if present & requested).
		  if (alpha != null) {
			memset(alpha, 0, sizeof(*alpha));
			if (wpi->alpha_ != null) {
			  alpha->bytes_ = wpi->alpha_->data_;
			  alpha->size_ = wpi->alpha_->payload_size_;
			}
		  }

		  // Get image chunk.
		  memset(image, 0, sizeof(*image));
		  if (wpi->vp8_ != null) {
			image->bytes_ = wpi->vp8_->data_;
			image->size_ = wpi->vp8_->payload_size_;
		  }

		  return WEBP_MUX_OK;
		}

		WebPMuxError WebPMuxGetFrame(const WebPMux* const mux, uint nth,
									 WebPData* const image, WebPData* const alpha,
									 uint* x_offset, uint* y_offset,
									 uint* duration) {
		  return MuxGetFrameTileInternal(mux, nth, image, alpha,
										 x_offset, y_offset, duration,
										 kChunks[FRAME_ID].chunkTag);
		}

		WebPMuxError WebPMuxGetTile(const WebPMux* const mux, uint nth,
									WebPData* const image, WebPData* const alpha,
									uint* x_offset, uint* y_offset) {
		  return MuxGetFrameTileInternal(mux, nth, image, alpha,
										 x_offset, y_offset, null,
										 kChunks[TILE_ID].chunkTag);
		}

		// Count number of chunks matching 'tag' in the 'chunk_list'.
		// If tag == NIL_TAG, any tag will be matched.
		static int CountChunks(WebPChunk* const chunk_list, uint tag) {
		  int count = 0;
		  WebPChunk* current;
		  for (current = chunk_list; current != null; current = current->next_) {
			if (tag == NIL_TAG || current->tag_ == tag) {
			  count++;  // Count chunks whose tags match.
			}
		  }
		  return count;
		}

		WebPMuxError WebPMuxNumNamedElements(const WebPMux* const mux, const char* tag,
											 int* num_elements) {
		  TAG_ID id;
		  WebPChunk** chunk_list;

		  if (mux == null || tag == null || num_elements == null) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  id = ChunkGetIdFromName(tag);
		  if (IsWPI(id)) {
			*num_elements = MuxImageCount(mux->images_, id);
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

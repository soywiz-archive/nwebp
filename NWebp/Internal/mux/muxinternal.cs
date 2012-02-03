using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal
{
	class muxinternal
	{

		int UNDEFINED_CHUNK_SIZE = (-1);

		ChunkInfo[] kChunks = new ChunkInfo[] {
		  {"vp8x",    mktag('V', 'P', '8', 'X'),  VP8X_ID,    VP8X_CHUNK_SIZE},
		  {"iccp",    mktag('I', 'C', 'C', 'P'),  ICCP_ID,    UNDEFINED_CHUNK_SIZE},
		  {"loop",    mktag('L', 'O', 'O', 'P'),  LOOP_ID,    LOOP_CHUNK_SIZE},
		  {"frame",   mktag('F', 'R', 'M', ' '),  FRAME_ID,   FRAME_CHUNK_SIZE},
		  {"tile",    mktag('T', 'I', 'L', 'E'),  TILE_ID,    TILE_CHUNK_SIZE},
		  {"alpha",   mktag('A', 'L', 'P', 'H'),  ALPHA_ID,   UNDEFINED_CHUNK_SIZE},
		  {"image",   mktag('V', 'P', '8', ' '),  IMAGE_ID,   UNDEFINED_CHUNK_SIZE},
		  {"meta",    mktag('M', 'E', 'T', 'A'),  META_ID,    UNDEFINED_CHUNK_SIZE},
		  {"unknown", mktag('U', 'N', 'K', 'N'),  UNKNOWN_ID, UNDEFINED_CHUNK_SIZE},

		  {null,      NIL_TAG,                    NIL_ID,     UNDEFINED_CHUNK_SIZE},
		  {"list",    mktag('L', 'I', 'S', 'T'),  LIST_ID,    UNDEFINED_CHUNK_SIZE}
		};

		//------------------------------------------------------------------------------
		// Life of a chunk object.
		partial class WebPChunk
		{
			void ChunkInit() {
			  this.tag_ = NIL_TAG;
			  this.data_ = null;
			  this.payload_size_ = 0;
			  this.owner_ = 0;
			  this.image_info_ = null;
			  this.next_ = null;
			}
		}

		WebPChunk* ChunkRelease(WebPChunk* chunk) {
		  WebPChunk* next;
		  if (chunk == null) return null;
		  free(chunk.image_info_);
		  if (chunk.owner_) {
			free((void*)chunk.data_);
		  }
		  next = chunk.next_;
		  ChunkInit(chunk);
		  return next;
		}

		//------------------------------------------------------------------------------
		// Chunk misc methods.

		TAG_ID ChunkGetIdFromName(char* what) {
		  int i;
		  if (what == null) return -1;
		  for (i = 0; kChunks[i].chunkName != null; ++i) {
			if (!strcmp(what, kChunks[i].chunkName)) return i;
		  }
		  return NIL_ID;
		}

		TAG_ID ChunkGetIdFromTag(uint tag) {
		  int i;
		  for (i = 0; kChunks[i].chunkTag != NIL_TAG; ++i) {
			if (tag == kChunks[i].chunkTag) return i;
		  }
		  return NIL_ID;
		}

		//------------------------------------------------------------------------------
		// Chunk search methods.

		// Returns next chunk in the chunk list with the given tag.
		static WebPChunk* ChunkSearchNextInList(WebPChunk* chunk, uint tag) {
		  while (chunk && chunk.tag_ != tag) {
			chunk = chunk.next_;
		  }
		  return chunk;
		}

		WebPChunk* ChunkSearchList(WebPChunk* first, uint nth, uint tag) {
		  uint iter = nth;
		  first = ChunkSearchNextInList(first, tag);
		  if (!first) return null;

		  while (--iter != 0) {
			WebPChunk* next_chunk = ChunkSearchNextInList(first.next_, tag);
			if (next_chunk == null) break;
			first = next_chunk;
		  }
		  return ((nth > 0) && (iter > 0)) ? null : first;
		}

		// Outputs a pointer to 'prev_chunk.next_',
		//   where 'prev_chunk' is the pointer to the chunk at position (nth - 1).
		// Returns 1 if nth chunk was found, 0 otherwise.
		static int ChunkSearchListToSet(WebPChunk** chunk_list, uint nth,
										WebPChunk*** location) {
		  uint count = 0;
		  assert(chunk_list);
		  *location = chunk_list;

		  while (*chunk_list) {
			WebPChunk* cur_chunk = *chunk_list;
			++count;
			if (count == nth) return 1;  // Found.
			chunk_list = &cur_chunk.next_;
			*location = chunk_list;
		  }

		  // *chunk_list is ok to be null if adding at last location.
		  return (nth == 0 || (count == nth - 1)) ? 1 : 0;
		}

		//------------------------------------------------------------------------------
		// Chunk writer methods.

		WebPMuxError ChunkAssignDataImageInfo(WebPChunk* chunk,
											  byte* data, uint data_size,
											  WebPImageInfo* image_info,
											  int copy_data, uint tag) {
		  // For internally allocated chunks, always copy data & make it owner of data.
		  if ((tag == kChunks[VP8X_ID].chunkTag) ||
			  (tag == kChunks[LOOP_ID].chunkTag)) {
			copy_data = 1;
		  }

		  ChunkRelease(chunk);
		  if (data == null) {
			data_size = 0;
		  } else if (data_size == 0) {
			data = null;
		  }

		  if (data != null) {
			if (copy_data) {
			  // Copy data.
			  chunk.data_ = (byte*)malloc(data_size);
			  if (chunk.data_ == null) return WEBP_MUX_MEMORY_ERROR;
			  memcpy((byte*)chunk.data_, data, data_size);
			  chunk.payload_size_ = data_size;

			  // Chunk is owner of data.
			  chunk.owner_ = 1;
			} else {
			  // Don't copy data.
			  chunk.data_ = data;
			  chunk.payload_size_ = data_size;
			}
		  }

		  if (tag == kChunks[IMAGE_ID].chunkTag) {
			chunk.image_info_ = image_info;
		  }

		  chunk.tag_ = tag;

		  return WEBP_MUX_OK;
		}

		WebPMuxError ChunkSetNth(WebPChunk* chunk, WebPChunk** chunk_list,
								 uint nth) {
		  WebPChunk* new_chunk;

		  if (!ChunkSearchListToSet(chunk_list, nth, &chunk_list)) {
			return WEBP_MUX_NOT_FOUND;
		  }

		  new_chunk = (WebPChunk*)malloc(sizeof(*new_chunk));
		  if (new_chunk == null) return WEBP_MUX_MEMORY_ERROR;
		  *new_chunk = *chunk;
		  new_chunk.next_ = *chunk_list;
		  *chunk_list = new_chunk;
		  return WEBP_MUX_OK;
		}

		//------------------------------------------------------------------------------
		// Chunk deletion method(s).

		WebPChunk* ChunkDelete(WebPChunk* chunk) {
		  WebPChunk* next = ChunkRelease(chunk);
		  free(chunk);
		  return next;
		}

		//------------------------------------------------------------------------------
		// Chunk serialization methods.

		uint ChunksListDiskSize(WebPChunk* chunk_list) {
		  uint size = 0;
		  while (chunk_list) {
			size += ChunkDiskSize(chunk_list);
			chunk_list = chunk_list.next_;
		  }
		  return size;
		}

		static byte* ChunkEmit(WebPChunk* chunk, byte* dst) {
		  assert(chunk);
		  assert(chunk.tag_ != NIL_TAG);
		  PutLE32(dst + 0, chunk.tag_);
		  PutLE32(dst + TAG_SIZE, chunk.payload_size_);
		  memcpy(dst + CHUNK_HEADER_SIZE, chunk.data_, chunk.payload_size_);
		  if (chunk.payload_size_ & 1)
			dst[CHUNK_HEADER_SIZE + chunk.payload_size_] = 0;  // Add padding.
		  return dst + ChunkDiskSize(chunk);
		}

		byte* ChunkListEmit(WebPChunk* chunk_list, byte* dst) {
		  while (chunk_list) {
			dst = ChunkEmit(chunk_list, dst);
			chunk_list = chunk_list.next_;
		  }
		  return dst;
		}

		//------------------------------------------------------------------------------
		// Life of a MuxImage object.

		void MuxImageInit(WebPMuxImage* wpi) {
		  assert(wpi);
		  memset(wpi, 0, sizeof(*wpi));
		}

		WebPMuxImage* MuxImageRelease(WebPMuxImage* wpi) {
		  WebPMuxImage* next;
		  if (wpi == null) return null;
		  ChunkDelete(wpi.header_);
		  ChunkDelete(wpi.alpha_);
		  ChunkDelete(wpi.vp8_);

		  next = wpi.next_;
		  MuxImageInit(wpi);
		  return next;
		}

		//------------------------------------------------------------------------------
		// MuxImage search methods.

		int MuxImageCount(WebPMuxImage* wpi_list, TAG_ID id) {
		  int count = 0;
		  WebPMuxImage* current;
		  for (current = wpi_list; current != null; current = current.next_) {
			WebPChunk** wpi_chunk_ptr = MuxImageGetListFromId(current, id);
			assert(wpi_chunk_ptr != null);

			if (*wpi_chunk_ptr != null &&
				(*wpi_chunk_ptr).tag_ == kChunks[id].chunkTag) {
			  ++count;
			}
		  }
		  return count;
		}

		// Outputs a pointer to 'prev_wpi.next_',
		//   where 'prev_wpi' is the pointer to the image at position (nth - 1).
		// Returns 1 if nth image was found, 0 otherwise.
		static int SearchImageToSet(WebPMuxImage** wpi_list, uint nth,
									WebPMuxImage*** location) {
		  uint count = 0;
		  assert(wpi_list);
		  *location = wpi_list;

		  while (*wpi_list) {
			WebPMuxImage* cur_wpi = *wpi_list;
			++count;
			if (count == nth) return 1;  // Found.
			wpi_list = &cur_wpi.next_;
			*location = wpi_list;
		  }

		  // *chunk_list is ok to be null if adding at last location.
		  return (nth == 0 || (count == nth - 1)) ? 1 : 0;
		}

		// Outputs a pointer to 'prev_wpi.next_',
		//   where 'prev_wpi' is the pointer to the image at position (nth - 1).
		// Returns 1 if nth image with given id was found, 0 otherwise.
		static int SearchImageToGetOrDelete(WebPMuxImage** wpi_list, uint nth,
											TAG_ID id, WebPMuxImage*** location) {
		  uint count = 0;
		  assert(wpi_list);
		  *location = wpi_list;

		  // Search makes sense only for the following.
		  assert(id == FRAME_ID || id == TILE_ID || id == IMAGE_ID);
		  assert(id != IMAGE_ID || nth == 1);

		  if (nth == 0) {
			nth = MuxImageCount(*wpi_list, id);
			if (nth == 0) return 0;  // Not found.
		  }

		  while (*wpi_list) {
			WebPMuxImage* cur_wpi = *wpi_list;
			WebPChunk** wpi_chunk_ptr = MuxImageGetListFromId(cur_wpi, id);
			assert(wpi_chunk_ptr != null);
			if ((*wpi_chunk_ptr).tag_ == kChunks[id].chunkTag) {
			  ++count;
			  if (count == nth) return 1;  // Found.
			}
			wpi_list = &cur_wpi.next_;
			*location = wpi_list;
		  }
		  return 0;  // Not found.
		}

		//------------------------------------------------------------------------------
		// MuxImage writer methods.

		WebPMuxError MuxImageSetNth(WebPMuxImage* wpi, WebPMuxImage** wpi_list,
									uint nth) {
		  WebPMuxImage* new_wpi;

		  if (!SearchImageToSet(wpi_list, nth, &wpi_list)) {
			return WEBP_MUX_NOT_FOUND;
		  }

		  new_wpi = (WebPMuxImage*)malloc(sizeof(*new_wpi));
		  if (new_wpi == null) return WEBP_MUX_MEMORY_ERROR;
		  *new_wpi = *wpi;
		  new_wpi.next_ = *wpi_list;
		  *wpi_list = new_wpi;
		  return WEBP_MUX_OK;
		}

		//------------------------------------------------------------------------------
		// MuxImage deletion methods.

		WebPMuxImage* MuxImageDelete(WebPMuxImage* wpi) {
		  // Delete the components of wpi. If wpi is null this is a noop.
		  WebPMuxImage* next = MuxImageRelease(wpi);
		  free(wpi);
		  return next;
		}

		void MuxImageDeleteAll(WebPMuxImage** wpi_list) {
		  while (*wpi_list) {
			*wpi_list = MuxImageDelete(*wpi_list);
		  }
		}

		WebPMuxError MuxImageDeleteNth(WebPMuxImage** wpi_list, uint nth,
									   TAG_ID id) {
		  assert(wpi_list);
		  if (!SearchImageToGetOrDelete(wpi_list, nth, id, &wpi_list)) {
			return WEBP_MUX_NOT_FOUND;
		  }
		  *wpi_list = MuxImageDelete(*wpi_list);
		  return WEBP_MUX_OK;
		}

		//------------------------------------------------------------------------------
		// MuxImage reader methods.

		WebPMuxError MuxImageGetNth(WebPMuxImage** wpi_list, uint nth,
									TAG_ID id, WebPMuxImage** wpi) {
		  assert(wpi_list);
		  assert(wpi);
		  if (!SearchImageToGetOrDelete((WebPMuxImage**)wpi_list, nth, id,
										(WebPMuxImage***)&wpi_list)) {
			return WEBP_MUX_NOT_FOUND;
		  }
		  *wpi = (WebPMuxImage*)*wpi_list;
		  return WEBP_MUX_OK;
		}

		//------------------------------------------------------------------------------
		// MuxImage serialization methods.

		// Size of an image.
		static uint MuxImageDiskSize(WebPMuxImage* wpi) {
		  uint size = 0;
		  if (wpi.header_ != null) size += ChunkDiskSize(wpi.header_);
		  if (wpi.alpha_ != null) size += ChunkDiskSize(wpi.alpha_);
		  if (wpi.vp8_ != null) size += ChunkDiskSize(wpi.vp8_);
		  return size;
		}

		uint MuxImageListDiskSize(WebPMuxImage* wpi_list) {
		  uint size = 0;
		  while (wpi_list) {
			size += MuxImageDiskSize(wpi_list);
			wpi_list = wpi_list.next_;
		  }
		  return size;
		}

		static byte* MuxImageEmit(WebPMuxImage* wpi, byte* dst) {
		  // Ordering of chunks to be emitted is strictly as follows:
		  // 1. Frame/Tile chunk (if present).
		  // 2. Alpha chunk (if present).
		  // 3. VP8 chunk.
		  assert(wpi);
		  if (wpi.header_ != null) dst = ChunkEmit(wpi.header_, dst);
		  if (wpi.alpha_ != null) dst = ChunkEmit(wpi.alpha_, dst);
		  if (wpi.vp8_ != null) dst = ChunkEmit(wpi.vp8_, dst);
		  return dst;
		}

		byte* MuxImageListEmit(WebPMuxImage* wpi_list, byte* dst) {
		  while (wpi_list) {
			dst = MuxImageEmit(wpi_list, dst);
			wpi_list = wpi_list.next_;
		  }
		  return dst;
		}

		//------------------------------------------------------------------------------
		// Helper methods for mux.

		WebPChunk** GetChunkListFromId(WebPMux* mux, TAG_ID id) {
		  assert(mux != null);
		  switch(id) {
			case VP8X_ID: return (WebPChunk**)&mux.vp8x_;
			case ICCP_ID: return (WebPChunk**)&mux.iccp_;
			case LOOP_ID: return (WebPChunk**)&mux.loop_;
			case META_ID: return (WebPChunk**)&mux.meta_;
			case UNKNOWN_ID: return (WebPChunk**)&mux.unknown_;
			default: return null;
		  }
		}

		WebPMuxError ValidateForImage(WebPMux* mux) {
		  int num_vp8 = MuxImageCount(mux.images_, IMAGE_ID);
		  int num_frames = MuxImageCount(mux.images_, FRAME_ID);
		  int num_tiles = MuxImageCount(mux.images_, TILE_ID);

		  if (num_vp8 == 0) {
			// No images in mux.
			return WEBP_MUX_NOT_FOUND;
		  } else if (num_vp8 == 1 && num_frames == 0 && num_tiles == 0) {
			// Valid case (single image).
			return WEBP_MUX_OK;
		  } else {
			// Frame/Tile case OR an invalid mux.
			return WEBP_MUX_INVALID_ARGUMENT;
		  }
		}

		static int IsNotCompatible(int feature, int num_items) {
		  return (feature != 0) != (num_items > 0);
		}

		WebPMuxError WebPMuxValidate(WebPMux* mux) {
		  int num_iccp;
		  int num_meta;
		  int num_loop_chunks;
		  int num_frames;
		  int num_tiles;
		  int num_vp8x;
		  int num_images;
		  int num_alpha;
		  uint flags;
		  WebPMuxError err;

		  // Verify mux is not null.
		  if (mux == null || mux.state_ == WEBP_MUX_STATE_ERROR) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  // No further checks if mux is partial.
		  if (mux.state_ == WEBP_MUX_STATE_PARTIAL) return WEBP_MUX_OK;

		  // Verify mux has at least one image.
		  if (mux.images_ == null) return WEBP_MUX_INVALID_ARGUMENT;

		  err = WebPMuxGetFeatures(mux, &flags);
		  if (err != WEBP_MUX_OK) return err;

		  // At most one color profile chunk.
		  err = WebPMuxNumNamedElements(mux, kChunks[ICCP_ID].chunkName, &num_iccp);
		  if (err != WEBP_MUX_OK) return err;
		  if (num_iccp > 1) return WEBP_MUX_INVALID_ARGUMENT;

		  // ICCP_FLAG and color profile chunk is consistent.
		  if (IsNotCompatible(flags & ICCP_FLAG, num_iccp)) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  // At most one XMP metadata.
		  err = WebPMuxNumNamedElements(mux, kChunks[META_ID].chunkName, &num_meta);
		  if (err != WEBP_MUX_OK) return err;
		  if (num_meta > 1) return WEBP_MUX_INVALID_ARGUMENT;

		  // META_FLAG and XMP metadata chunk is consistent.
		  if (IsNotCompatible(flags & META_FLAG, num_meta)) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  // At most one loop chunk.
		  err = WebPMuxNumNamedElements(mux, kChunks[LOOP_ID].chunkName,
										&num_loop_chunks);
		  if (err != WEBP_MUX_OK) return err;
		  if (num_loop_chunks > 1) return WEBP_MUX_INVALID_ARGUMENT;

		  // Animation: ANIMATION_FLAG, loop chunk and frame chunk(s) are consistent.
		  err = WebPMuxNumNamedElements(mux, kChunks[FRAME_ID].chunkName, &num_frames);
		  if (err != WEBP_MUX_OK) return err;
		  if ((flags & ANIMATION_FLAG) &&
			  ((num_loop_chunks == 0) || (num_frames == 0))) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  } else if (((num_loop_chunks == 1) || (num_frames > 0)) &&
					 !(flags & ANIMATION_FLAG)) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  // Tiling: TILE_FLAG and tile chunk(s) are consistent.
		  err = WebPMuxNumNamedElements(mux, kChunks[TILE_ID].chunkName, &num_tiles);
		  if (err != WEBP_MUX_OK) return err;
		  if (IsNotCompatible(flags & TILE_FLAG, num_tiles)) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  // Verify either VP8X chunk is present OR there is only one elem in
		  // mux.images_.
		  err = WebPMuxNumNamedElements(mux, kChunks[VP8X_ID].chunkName, &num_vp8x);
		  if (err != WEBP_MUX_OK) return err;
		  err = WebPMuxNumNamedElements(mux, kChunks[IMAGE_ID].chunkName, &num_images);
		  if (err != WEBP_MUX_OK) return err;

		  if (num_vp8x > 1) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  } else if ((num_vp8x == 0) && (num_images != 1)) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  // ALPHA_FLAG & alpha chunk(s) are consistent.
		  err = WebPMuxNumNamedElements(mux, kChunks[ALPHA_ID].chunkName, &num_alpha);
		  if (err != WEBP_MUX_OK) return err;
		  if (IsNotCompatible(flags & ALPHA_FLAG, num_alpha)) {
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  // num_images & num_alpha_chunks are consistent.
		  if ((num_alpha > 0) && (num_alpha != num_images)) {
			// Note that "num_alpha > 0" is the correct check but "flags && ALPHA_FLAG"
			// is NOT, because ALPHA_FLAG is based on first image only.
			return WEBP_MUX_INVALID_ARGUMENT;
		  }

		  return WEBP_MUX_OK;
		}

		//------------------------------------------------------------------------------


	}
}

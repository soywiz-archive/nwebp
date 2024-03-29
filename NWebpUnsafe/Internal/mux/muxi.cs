﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if false
namespace NWebp.Internal
{
	//------------------------------------------------------------------------------
	// Defines and constants.

	// Object to store metadata about images.
	class WebPImageInfo
	{
		uint    x_offset_;
		uint    y_offset_;
		uint    duration_;
		uint    width_;
		uint    height_;
	}

	/// <summary>
	/// Chunk object.
	/// </summary>
	partial class WebPChunk
	{
		/// <summary>
		/// 
		/// </summary>
		public uint        tag_;

		/// <summary>
		/// 
		/// </summary>
		public uint        payload_size_;

		/// <summary>
		/// 
		/// </summary>
		public WebPImageInfo  image_info_;

		/// <summary>
		/// True if *data_ memory is owned internally.
		/// VP8X, Loop, and other internally created chunks
		/// like frame/tile are always owned.
		/// </summary>
		public int             owner_;

		/// <summary>
		/// 
		/// </summary>
		public byte* data_;

		/// <summary>
		/// 
		/// </summary>
		public WebPChunk      next_;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="c1"></param>
		/// <param name="c2"></param>
		/// <param name="c3"></param>
		/// <param name="c4"></param>
		/// <returns></returns>
		static public uint mktag(byte c1, byte c2, byte c3, byte c4)
		{
			return ((uint)c1 | (uint)(c2 << 8) | (uint)(c3 << 16) | (uint)(c4 << 24));
		}
	}

	/// <summary>
	/// MuxImage object. Store a full webp image (including frame/tile chunk, alpha
	/// chunk and VP8 chunk),
	/// </summary>
	partial class WebPMuxImage
	{
		/// <summary>
		/// Corresponds to FRAME_ID/TILE_ID.
		/// </summary>
		WebPChunk  header_;

		/// <summary>
		/// Corresponds to ALPHA_ID.
		/// </summary>
		WebPChunk  alpha_;

		/// <summary>
		/// Corresponds to IMAGE_ID.
		/// </summary>
		WebPChunk  vp8_;        

		/// <summary>
		/// True if only some of the chunks are filled.
		/// </summary>
		int         is_partial_; 

		/// <summary>
		/// 
		/// </summary>
		WebPMuxImage next_;
	}

	/// <summary>
	/// Main mux object. Stores data chunks.
	/// </summary>
	partial class WebPMux
	{
		WebPMuxState    state_;
		WebPMuxImage   images_;
		WebPChunk      iccp_;
		WebPChunk      meta_;
		WebPChunk      loop_;
		WebPChunk      vp8x_;

		WebPChunk  unknown_;
	};

	partial class Global
	{
		int CHUNKS_PER_FRAME  = 2;
		int CHUNKS_PER_TILE   = 2;

		/// <summary>
		/// Maximum chunk payload (data) size such that adding the header and padding
		/// won't overflow an uint32.
		/// </summary>
		uint MAX_CHUNK_PAYLOAD = (~0U - CHUNK_HEADER_SIZE - 1);

		uint NIL_TAG = 0x00000000u;  // To signal void chunk.

	}

	/// <summary>
	/// 
	/// </summary>
	public enum TAG_ID
	{
		VP8X_ID = 0,
		ICCP_ID,
		LOOP_ID,
		FRAME_ID,
		TILE_ID,
		ALPHA_ID,
		IMAGE_ID,
		META_ID,
		UNKNOWN_ID,

		NIL_ID,
		LIST_ID
	}

	/// <summary>
	/// 
	/// </summary>
	public partial class ChunkInfo
	{
		/// <summary>
		/// char*   chunkName;
		/// </summary>
		string chunkName;

		/// <summary>
		/// 
		/// </summary>
		uint      chunkTag;

		/// <summary>
		/// 
		/// </summary>
		TAG_ID        chunkId;

		/// <summary>
		/// 
		/// </summary>
		uint      chunkSize;
	}

	//extern ChunkInfo kChunks[LIST_ID + 1];

	/// <summary>
	/// Helper functions.
	/// </summary>
	unsafe partial class Helper
	{
		static uint GetLE32(byte* data) {
			return (uint)(data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24));
		}

		static void PutLE16(byte* data, ushort val) {
			data[0] = (byte)((val >> 0) & 0xff);
			data[1] = (byte)((val >> 8) & 0xff);
		}

		static void PutLE32(byte* data, uint val) {
			PutLE16(data, (ushort)val);
			PutLE16(data + 2, (ushort)(val >> 16));
		}

		static uint SizeWithPadding(uint chunk_size) {
			return CHUNK_HEADER_SIZE + ((chunk_size + 1) & ~1U);
		}
	}

	//------------------------------------------------------------------------------
	// Chunk object management.

	/*
	// Initialize.
	void ChunkInit(WebPChunk* chunk);

	// Get chunk id from chunk name.
	TAG_ID ChunkGetIdFromName(char* what);

	// Get chunk id from chunk tag.
	TAG_ID ChunkGetIdFromTag(uint tag);

	// Search for nth chunk with given 'tag' in the chunk list.
	// nth = 0 means "last of the list".
	WebPChunk* ChunkSearchList(WebPChunk* first, uint nth, uint tag);

	// Fill the chunk with the given data & image_info.
	WebPMuxError ChunkAssignDataImageInfo(WebPChunk* chunk, byte* data, uint data_size, WebPImageInfo* image_info, int copy_data, uint tag);

	// Sets 'chunk' at nth position in the 'chunk_list'.
	// nth = 0 has the special meaning "last of the list".
	WebPMuxError ChunkSetNth(WebPChunk* chunk, WebPChunk** chunk_list, uint nth);

	// Releases chunk and returns chunk.next_.
	WebPChunk* ChunkRelease(WebPChunk* chunk);

	// Deletes given chunk & returns chunk.next_.
	WebPChunk* ChunkDelete(WebPChunk* chunk);
	*/

	// Size of a chunk including header and padding.
	partial class WebPChunk
	{
		static uint ChunkDiskSize() {
			Global.assert(this.payload_size_ < MAX_CHUNK_PAYLOAD);
			return SizeWithPadding(this.payload_size_);
		}
	}

	/*
	// Total size of a list of chunks.
	uint ChunksListDiskSize(ref WebPChunk chunk_list);

	// Write out the given list of chunks into 'dst'.
	byte* ChunkListEmit(ref WebPChunk chunk_list, byte* dst);

	//------------------------------------------------------------------------------
	// MuxImage object management.

	// Initialize.
	void MuxImageInit(WebPMuxImage* wpi);

	// Releases image 'wpi' and returns wpi.next.
	WebPMuxImage* MuxImageRelease(WebPMuxImage* wpi);

	// Delete image 'wpi' and return the next image in the list or null.
	// 'wpi' can be null.
	WebPMuxImage* MuxImageDelete(WebPMuxImage* wpi);

	// Delete all images in 'wpi_list'.
	void MuxImageDeleteAll(WebPMuxImage** wpi_list);

	// Count number of images matching 'tag' in the 'wpi_list'.
	// If tag == NIL_TAG, any tag will be matched.
	int MuxImageCount(WebPMuxImage* wpi_list, TAG_ID id);
	*/

	/// <summary>
	/// Check if given ID corresponds to an image related chunk.
	/// </summary>
	unsafe partial class Global
	{
		static int IsWPI(TAG_ID id) {
			switch (id) {
				case TAG_ID.FRAME_ID:
				case TAG_ID.TILE_ID:
				case TAG_ID.ALPHA_ID:
				case TAG_ID.IMAGE_ID:  return 1;
				default:        return 0;
			}
		}
	}

	/*
	// Get a reference to appropriate chunk list within an image given chunk tag.
	static WebPChunk** MuxImageGetListFromId(WebPMuxImage* wpi, TAG_ID id) {
		assert(wpi != null);
		switch (id) {
		case FRAME_ID:
		case TILE_ID: return &wpi.header_;
		case ALPHA_ID: return &wpi.alpha_;
		case IMAGE_ID: return &wpi.vp8_;
		default: return null;
		}
	}

	// Sets 'wpi' at nth position in the 'wpi_list'.
	// nth = 0 has the special meaning "last of the list".
	WebPMuxError MuxImageSetNth(WebPMuxImage* wpi, WebPMuxImage** wpi_list,
								uint nth);

	// Delete nth image in the image list with given tag.
	WebPMuxError MuxImageDeleteNth(WebPMuxImage** wpi_list, uint nth,
									TAG_ID id);

	// Get nth image in the image list with given tag.
	WebPMuxError MuxImageGetNth(WebPMuxImage** wpi_list, uint nth,
								TAG_ID id, WebPMuxImage** wpi);

	// Total size of a list of images.
	uint MuxImageListDiskSize(WebPMuxImage* wpi_list);

	// Write out the given list of images into 'dst'.
	byte* MuxImageListEmit(WebPMuxImage* wpi_list, byte* dst);

	//------------------------------------------------------------------------------
	// Helper methods for mux.

	// Returns the list where chunk with given ID is to be inserted in mux.
	// Return value is null if this chunk should be inserted in mux.images_ list
	// or if 'id' is not known.
	WebPChunk** GetChunkListFromId(WebPMux* mux, TAG_ID id);

	// Validates that the given mux has a single image.
	WebPMuxError ValidateForImage(WebPMux* mux);

	// Validates the given mux object.
	WebPMuxError WebPMuxValidate(WebPMux* mux);

	//------------------------------------------------------------------------------
	*/
}
#endif
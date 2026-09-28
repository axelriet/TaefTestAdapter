// Memory leak detection for TAEF tests based on the checkpoints of the CRT debug heap.
//
// Usage: take a checkpoint in TEST_METHOD_SETUP (Start()) and call VerifyNoMemoryLeaks() at the end of the test body.
// TAEF does not change the result of a test because of a failure in TEST_METHOD_CLEANUP, so the check which is
// supposed to fail the test must run inside the test itself. Only allocations of the test DLL's own CRT are
// tracked; TAEF's allocations (e.g. of WEX::Common::String) use the heap of the TAEF DLLs.
//
// Leaks are only detected in Debug builds (the CRT debug heap does not exist in Release builds).
#pragma once

#include <crtdbg.h>
#include "WexTestClass.h"

namespace TaefSamples
{
    class LeakDetector
    {
    public:
        void Start()
        {
#ifdef _DEBUG
            _CrtMemCheckpoint(&m_start);
#endif
            m_started = true;
        }

        // number of bytes (and blocks) allocated since Start() which have not been freed
        size_t GetLeakedBytes(size_t& leakedBlocks) const
        {
            leakedBlocks = 0;
#ifdef _DEBUG
            if (m_started)
            {
                _CrtMemState now;
                _CrtMemState difference;
                _CrtMemCheckpoint(&now);
                if (_CrtMemDifference(&difference, &m_start, &now))
                {
                    leakedBlocks = difference.lCounts[_NORMAL_BLOCK];
                    return difference.lSizes[_NORMAL_BLOCK];
                }
            }
#endif
            return 0;
        }

        void VerifyNoMemoryLeaks() const
        {
#ifdef _DEBUG
            size_t leakedBlocks = 0;
            size_t leakedBytes = GetLeakedBytes(leakedBlocks);
            VERIFY_ARE_EQUAL(static_cast<size_t>(0), leakedBytes,
                WEX::Common::String().Format(L"memory leaked by the test: %Iu bytes in %Iu blocks", leakedBytes, leakedBlocks));
#else
            WEX::Logging::Log::Comment(L"Memory leak detection is only performed in Debug builds.");
#endif
        }

    private:
#ifdef _DEBUG
        _CrtMemState m_start = {};
#endif
        bool m_started = false;
    };
}
